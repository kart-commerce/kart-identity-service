namespace Kart.Identity.Application.Features.RegisterUser;

/// <summary>api-contract.yaml `components.schemas.TokenPair`.</summary>
public sealed record RegisterUserResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);
