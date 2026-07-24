using FluentValidation;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Kart.Identity.Api.Middleware;

/// <summary>
/// Maps known Application-layer exceptions to api-contract.yaml's `Problem` shape.
/// Any exception not recognized here is left unhandled (returns false), falling
/// through to ASP.NET Core's own problem-details/developer-exception-page handling.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
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
            _ => (0, null)
        };

        if (problem is null)
        {
            return false;
        }

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
