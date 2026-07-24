using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Mints an RS256-signed access token carrying role/scope claims resolved at mint
/// time (requirement-spec.md §2's RBAC FR). Owned by Application, implemented by
/// Infrastructure — the private signing key never leaves Infrastructure
/// (design-decisions.md, "JWT Signing Algorithm").
/// </summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(Guid userId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> scopes);
}
