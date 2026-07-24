using Kart.Identity.Application.Features.IssueServicePrincipalToken;
using Kart.Identity.Application.Features.Login;
using Kart.Identity.Application.Features.RotateRefreshToken;
using Kart.Identity.Application.Features.RegisterUser;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1` native AuthN/registration paths.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/register", async (RegisterRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.Password, request.DisplayName);
            var response = await sender.Send(command, cancellationToken);
            return Results.Created((string?)null, response);
        })
        .WithName("RegisterUser")
        .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/v1/auth/login", async (LoginRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new LoginCommand(request.Email, request.Password, ipAddress);
            var result = await sender.Send(command, cancellationToken);

            return result switch
            {
                AuthenticatedLoginResult authenticated => Results.Ok(authenticated),
                MfaChallengeLoginResult challenge => Results.Json(challenge, statusCode: StatusCodes.Status202Accepted),
                _ => throw new InvalidOperationException($"Unhandled {nameof(LoginResult)} subtype: {result.GetType()}")
            };
        })
        .WithName("Login")
        .Produces<AuthenticatedLoginResult>(StatusCodes.Status200OK)
        .Produces<MfaChallengeLoginResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status423Locked)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapPost("/v1/auth/token", async (HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var command = new IssueServicePrincipalTokenCommand(
                form["grant_type"].ToString(),
                form["client_id"].ToString(),
                form["client_secret"].ToString(),
                form["scope"].ToString() is { Length: > 0 } scope ? scope : null);
            var response = await sender.Send(command, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("IssueServicePrincipalToken")
        .Accepts<IFormCollection>("application/x-www-form-urlencoded")
        .Produces<IssueServicePrincipalTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/auth/refresh", async (RefreshRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new RotateRefreshTokenCommand(request.RefreshToken), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .Produces<RotateRefreshTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private sealed record RegisterRequest(string Email, string Password, string? DisplayName);

    private sealed record LoginRequest(string Email, string Password);

    private sealed record RefreshRequest(string RefreshToken);
}
