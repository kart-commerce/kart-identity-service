namespace Kart.Identity.Application.Features.SocialLoginCallback;

/// <summary>api-contract.yaml `components.schemas.TokenPair`.</summary>
public sealed record SocialLoginCallbackResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);
