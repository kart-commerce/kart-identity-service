using MediatR;

namespace Kart.Identity.Application.Features.RotateRefreshToken;

/// <summary>
/// api-contract.yaml POST /auth/refresh — rotates a single-use refresh token
/// for a new access/refresh pair. Named `RotateRefreshToken` (not `RefreshToken`,
/// tickets.md's literal use-case name) purely to avoid colliding with the
/// `Kart.Identity.Domain.Entities.RefreshToken` type — a feature namespace
/// identical to that entity's simple name makes every unqualified reference to
/// the entity elsewhere in Application ambiguous with this namespace.
/// </summary>
public sealed record RotateRefreshTokenCommand(string RefreshToken) : IRequest<RotateRefreshTokenResponse>;
