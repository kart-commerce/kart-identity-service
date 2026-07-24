namespace Kart.Identity.Application.Features.EnterpriseOidcCallback;

/// <summary>api-contract.yaml `components.schemas.TokenPair`.</summary>
public sealed record EnterpriseOidcCallbackResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);
