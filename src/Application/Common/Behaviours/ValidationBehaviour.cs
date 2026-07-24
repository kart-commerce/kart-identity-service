using FluentValidation;
using MediatR;

namespace Kart.Identity.Application.Common.Behaviours;

/// <summary>
/// Runs every registered FluentValidation validator for the incoming request before
/// its handler executes, aggregating all failures into a single
/// <see cref="ValidationException"/> (api-contract.yaml's 400 responses) rather than
/// letting each handler validate itself ad hoc.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
            throw new ValidationException(failures);
        }

        return await next();
    }
}
