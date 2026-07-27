using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Common.Behaviours;

/// <summary>
/// observability-standards.md: every command/query gets a structured Information log
/// on completion, tagged with its own name and duration — the generic backbone that
/// gives every MediatR request 100% log coverage regardless of whether its handler adds
/// its own business-milestone log. Deliberately never logs the request/response objects
/// themselves (commands like <c>LoginCommand</c> carry a raw password) — only the
/// request's type name, never its field values, so this can't leak a secret by construction.
/// Exceptions are intentionally left unlogged here and rethrown as-is: they're logged once,
/// at the true boundary (the Api layer's <c>GlobalExceptionHandler</c>), not duplicated at
/// every pipeline layer they pass through.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        logger.LogInformation(
            "{RequestName} completed in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
