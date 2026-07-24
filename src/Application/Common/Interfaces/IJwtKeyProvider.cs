using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure (dependency inversion,
/// coding-standards.md). The private RS256 signing key never leaves Identity
/// (design-decisions.md, "JWT Signing Algorithm") — this abstraction only
/// exposes the public half for JWKS discovery.
/// </summary>
public interface IJwtKeyProvider
{
    IReadOnlyList<JsonWebKey> GetPublicSigningKeys();
}
