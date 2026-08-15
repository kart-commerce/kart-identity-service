using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Common.Behaviours;

/// <summary>
/// Runs every registered FluentValidation validator for the incoming request before
/// its handler executes, aggregating all failures into a single
/// <see cref="ValidationException"/> (api-contract.yaml's 400 responses) rather than
/// letting each handler validate itself ad hoc.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(request, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            var requestName = typeof(TRequest).Name;

            // Checkpoint-logging taxonomy stage 4 ("<Rule>ValidationFailed", logged at Warning
            // with the reason before throwing) generalized here for every FluentValidation
            // validator on the platform, rather than duplicated per handler — the
            // ValidationException itself is still logged once more, generically, at the API
            // boundary by KartExceptionHandler; this line is the one that's greppable by Stage
            // and carries the actual field-level reasons.
            logger.LogWarning(
                "Stage {Stage}: {RequestName} rejected — {Errors}",
                $"{requestName}ValidationFailed",
                requestName,
                string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

            throw new ValidationException(failures);
        }

        return await next();
    }
}
