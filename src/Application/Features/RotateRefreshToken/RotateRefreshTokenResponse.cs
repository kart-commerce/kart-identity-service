namespace Kart.Identity.Application.Features.RotateRefreshToken;

/// <summary>api-contract.yaml POST /auth/refresh 200 — `components.schemas.TokenPair`.</summary>
public sealed record RotateRefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);
