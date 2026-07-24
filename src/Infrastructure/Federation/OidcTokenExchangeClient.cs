using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// Exchanges an OIDC authorization code for an id_token at the provider's token
/// endpoint (an outbound HTTP call, unlike SAML's inline signature check) and
/// validates it — design-decisions.md, "Resilience Pattern for External IdP
/// Calls": per-provider circuit breaker + concurrency-limiter (bulkhead) +
/// timeout budget, keyed on <see cref="OidcProviderDescriptor.ProviderKey"/> so
/// one slow/down IdP can't cascade into another's federation traffic.
/// </summary>
public sealed class OidcTokenExchangeClient(IHttpClientFactory httpClientFactory) : IOidcTokenExchangeClient
{
    private const string HttpClientName = "oidc-token-exchange";
    private static readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> Pipelines = new();

    public async Task<OidcIdentityResult> ExchangeCodeAsync(
        OidcProviderDescriptor provider, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pipeline = Pipelines.GetOrAdd(provider.ProviderKey, _ => BuildPipeline());
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await pipeline.ExecuteAsync(
                async ct =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, provider.TokenEndpoint)
                    {
                        Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["grant_type"] = "authorization_code",
                            ["code"] = code,
                            ["redirect_uri"] = provider.RedirectUri,
                            ["client_id"] = provider.ClientId,
                            ["client_secret"] = provider.ClientSecret
                        })
                    };
                    return await httpClient.SendAsync(request, ct);
                },
                cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            throw new InvalidOidcTokenException($"IdP '{provider.ProviderKey}' circuit is open — too many recent failures");
        }
        catch (TimeoutRejectedException)
        {
            throw new InvalidOidcTokenException($"timed out exchanging the authorization code with IdP '{provider.ProviderKey}'");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOidcTokenException($"token exchange request failed: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOidcTokenException($"token endpoint returned {(int)response.StatusCode}");
        }

        var body = await response.Content.ReadFromJsonAsync<TokenEndpointResponse>(cancellationToken)
            ?? throw new InvalidOidcTokenException("token endpoint returned an empty/malformed body");

        if (string.IsNullOrEmpty(body.IdToken))
        {
            throw new InvalidOidcTokenException("token endpoint response has no id_token");
        }

        return ValidateAndExtract(body.IdToken, provider, now);
    }

    private static OidcIdentityResult ValidateAndExtract(string idToken, OidcProviderDescriptor provider, DateTimeOffset now)
    {
        X509Certificate2 certificate;
        try
        {
            certificate = X509Certificate2.CreateFromPem(provider.SigningCertificatePem);
        }
        catch (CryptographicException)
        {
            throw new InvalidOidcTokenException("IdP signing certificate is misconfigured");
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = provider.Issuer,
            ValidateAudience = true,
            ValidAudience = provider.ClientId,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                (notBefore is null || now >= notBefore) && expires is not null && now < expires,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new X509SecurityKey(certificate)
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new InvalidOidcTokenException($"id_token validation failed: {ex.Message}");
        }

        var subject = principal.FindFirst("sub")?.Value
            ?? throw new InvalidOidcTokenException("id_token has no sub claim");
        var email = principal.FindFirst("email")?.Value;
        var groupClaims = principal.FindAll("groups").Select(c => c.Value).ToList();

        return new OidcIdentityResult(subject, email, groupClaims);
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline() =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddConcurrencyLimiter(permitLimit: 10, queueLimit: 0)
            .AddTimeout(TimeSpan.FromSeconds(5))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => ValueTask.FromResult(args.Outcome switch
                {
                    { Exception: HttpRequestException or TimeoutRejectedException } => true,
                    { Result: { } result } => !result.IsSuccessStatusCode,
                    _ => false
                })
            })
            .Build();

    private sealed record TokenEndpointResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}
