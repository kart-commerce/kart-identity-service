namespace Kart.Identity.Application.Features.VerifyMfa;

/// <summary>api-contract.yaml POST /auth/mfa/verify 200 — `components.schemas.TokenPair`.</summary>
public sealed record VerifyMfaResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);
