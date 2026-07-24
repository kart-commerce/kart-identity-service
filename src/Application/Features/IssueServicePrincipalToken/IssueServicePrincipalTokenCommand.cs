using MediatR;

namespace Kart.Identity.Application.Features.IssueServicePrincipalToken;

/// <summary>
/// api-contract.yaml POST /auth/token — OAuth2 Client Credentials grant
/// (requirement-spec.md §2). <see cref="Scope"/> is the raw, still space-delimited
/// value from the form body; null/empty means "no scope requested."
/// </summary>
public sealed record IssueServicePrincipalTokenCommand(
    string GrantType,
    string ClientId,
    string ClientSecret,
    string? Scope) : IRequest<IssueServicePrincipalTokenResponse>;
