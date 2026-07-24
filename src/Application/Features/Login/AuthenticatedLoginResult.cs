namespace Kart.Identity.Application.Features.Login;

/// <summary>api-contract.yaml POST /auth/login 200 — `components.schemas.TokenPair`.</summary>
public sealed record AuthenticatedLoginResult(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes) : LoginResult;
