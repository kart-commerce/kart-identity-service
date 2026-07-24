using FluentValidation;

namespace Kart.Identity.Application.Features.IssueServicePrincipalToken;

/// <summary>api-contract.yaml POST /auth/token request schema: grant_type is fixed to the one supported grant.</summary>
public sealed class IssueServicePrincipalTokenCommandValidator : AbstractValidator<IssueServicePrincipalTokenCommand>
{
    public IssueServicePrincipalTokenCommandValidator()
    {
        RuleFor(x => x.GrantType).Equal("client_credentials");
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ClientSecret).NotEmpty();
    }
}
