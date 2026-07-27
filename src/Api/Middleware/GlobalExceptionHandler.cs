using FluentValidation;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Kart.Identity.Api.Middleware;

/// <summary>
/// Maps known Application-layer exceptions to api-contract.yaml's `Problem` shape.
/// Any exception not recognized here is left unhandled (returns false), falling
/// through to ASP.NET Core's own problem-details/developer-exception-page handling.
/// This is also the single place every exception reaching an HTTP request's boundary is
/// logged (observability-standards.md: Warning for a handled/expected business
/// exception, Error for anything unrecognized) — deliberately not duplicated inside
/// <c>LoggingBehaviour</c>, which every one of these requests also passes through.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, problem) = exception switch
        {
            ValidationException validationException =>
                (StatusCodes.Status400BadRequest, ToValidationProblem(validationException)),
            EmailAlreadyRegisteredException =>
                (StatusCodes.Status409Conflict, new Problem("email_already_registered", exception.Message)),
            InvalidCredentialsException =>
                (StatusCodes.Status401Unauthorized, new Problem("invalid_credentials", exception.Message)),
            AccountLockedException =>
                (StatusCodes.Status423Locked, new Problem("account_locked", exception.Message)),
            LoginRateLimitExceededException =>
                (StatusCodes.Status429TooManyRequests, new Problem("rate_limited", exception.Message)),
            InvalidOrExpiredMfaCodeException =>
                (StatusCodes.Status400BadRequest, new Problem("invalid_or_expired_code", exception.Message)),
            InvalidMfaChallengeException =>
                (StatusCodes.Status401Unauthorized, new Problem("invalid_mfa_challenge", exception.Message)),
            InvalidServicePrincipalCredentialsException =>
                (StatusCodes.Status401Unauthorized, new Problem("invalid_client", exception.Message)),
            RefreshTokenReuseDetectedException =>
                (StatusCodes.Status401Unauthorized, new Problem("refresh_token_reuse_detected", exception.Message)),
            RefreshTokenRaceLostException =>
                (StatusCodes.Status409Conflict, new Problem("refresh_token_race_lost", exception.Message)),
            UserNotFoundException =>
                (StatusCodes.Status404NotFound, new Problem("user_not_found", exception.Message)),
            InvalidOrExpiredPasswordResetTokenException =>
                (StatusCodes.Status400BadRequest, new Problem("invalid_or_expired_reset_token", exception.Message)),
            EnterpriseIdpNotConfiguredException =>
                (StatusCodes.Status404NotFound, new Problem("idp_not_configured", exception.Message)),
            InvalidSamlAssertionException =>
                (StatusCodes.Status401Unauthorized, new Problem("invalid_saml_assertion", exception.Message)),
            SamlAssertionReplayException =>
                (StatusCodes.Status409Conflict, new Problem("saml_assertion_replay", exception.Message)),
            InvalidOidcTokenException =>
                (StatusCodes.Status401Unauthorized, new Problem("invalid_oidc_token", exception.Message)),
            SocialIdpNotConfiguredException =>
                (StatusCodes.Status404NotFound, new Problem("idp_not_configured", exception.Message)),
            _ => (0, null)
        };

        if (problem is null)
        {
            // Not one of the recognized/expected exception types above — a genuine
            // unhandled failure surfacing at the API boundary.
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return false;
        }

        // A recognized business exception, already mapped to a client-safe Problem
        // above — a handled degraded path, not a bug, hence Warning rather than Error.
        logger.LogWarning(
            exception,
            "Request rejected with {ProblemCode} ({StatusCode}) for {Method} {Path}",
            problem.Code,
            statusCode,
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static Problem ToValidationProblem(ValidationException exception)
    {
        var details = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, object? (g) => g.Select(e => e.ErrorMessage).ToArray());

        return new Problem("validation_error", "One or more validation errors occurred.", details);
    }
}
