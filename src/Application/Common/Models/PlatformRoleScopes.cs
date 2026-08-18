using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// ADR-0025: the platform's role→scope mapping, resolved at human-token-mint
/// time and embedded in the JWT's `scopes` claim alongside `roles`
/// (JwtAccessTokenGenerator). Sibling to <see cref="PlatformRoleClaims"/>
/// (role→claim-value) — this is role→scope-value. Currently only `Admin` and
/// `SupportAgent` resolve to a scope (`ai-assistant.query`); `Customer` and
/// `PartnerApi` resolve to no scopes here — `PartnerApi`'s scopes continue to
/// come from the separate OAuth2 client-credentials request-scope pass-through
/// (IssueServicePrincipalTokenCommandHandler), unaffected by this mapping.
/// </summary>
public static class PlatformRoleScopes
{
    private static readonly IReadOnlyDictionary<PlatformRole, IReadOnlyList<string>> RoleScopes =
        new Dictionary<PlatformRole, IReadOnlyList<string>>
        {
            [PlatformRole.Customer] = [],
            [PlatformRole.SupportAgent] = ["ai-assistant.query"],
            [PlatformRole.Admin] = ["ai-assistant.query"],
            [PlatformRole.PartnerApi] = []
        };

    /// <summary>
    /// Resolves the de-duplicated union of scopes every role in
    /// <paramref name="roles"/> grants. Takes the plural shape because a
    /// principal can hold more than one concurrently-granted role (one
    /// `UserRoles` row per role) — matching how `roles` is already resolved as
    /// a collection at every human-token-mint call site (LoginCommandHandler,
    /// RotateRefreshTokenCommandHandler, ...).
    /// </summary>
    public static IReadOnlyList<string> ResolveScopes(IEnumerable<PlatformRole> roles) =>
        roles.SelectMany(role => RoleScopes[role]).Distinct().ToArray();
}
