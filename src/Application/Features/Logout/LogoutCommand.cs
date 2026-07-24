using MediatR;

namespace Kart.Identity.Application.Features.Logout;

/// <summary>
/// api-contract.yaml POST /auth/logout. <paramref name="Jti"/>/<paramref name="AccessTokenExpiresAt"/>
/// are read off the presented bearer token's own claims by the endpoint, not
/// supplied by the caller.
/// </summary>
public sealed record LogoutCommand(Guid UserId, string Jti, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken) : IRequest;
