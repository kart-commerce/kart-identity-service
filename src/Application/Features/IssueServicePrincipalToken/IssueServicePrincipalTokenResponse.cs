namespace Kart.Identity.Application.Features.IssueServicePrincipalToken;

/// <summary>api-contract.yaml POST /auth/token 200 — `components.schemas.ServicePrincipalToken`. No refresh token for this grant (no user session to rotate against).</summary>
public sealed record IssueServicePrincipalTokenResponse(string AccessToken, string TokenType, int ExpiresIn, IReadOnlyList<string> Scopes);
