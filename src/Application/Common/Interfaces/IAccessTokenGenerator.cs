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
    /// <summary>
    /// <paramref name="subject"/> becomes the JWT `sub` claim — a user's
    /// <c>Guid</c> for native/federated logins, or a service principal's
    /// `client_id` (not a Guid) for the Client Credentials flow (IDN-7);
    /// deliberately a string rather than <c>Guid</c> so both share this one
    /// minting path (single-issuer invariant, requirement-spec.md §4).
    /// </summary>
    AccessToken Generate(string subject, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> scopes);
}
