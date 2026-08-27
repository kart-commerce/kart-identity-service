using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// requirement-spec.md §2's four-role claim vocabulary (`roles: [...]` embedded in
/// the JWT) — the single place `PlatformRole` maps to the string an access token
/// actually carries, shared by every Application feature that mints or checks
/// roles (`RegisterUser`, `Login`, ...).
/// </summary>
public static class PlatformRoleClaims
{
    public static string ToClaimValue(PlatformRole role) => role switch
    {
        PlatformRole.Customer => "customer",
        PlatformRole.SupportAgent => "support_agent",
        PlatformRole.Admin => "admin",
        PlatformRole.PartnerApi => "partner_api",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>
    /// The reverse of <see cref="ToClaimValue"/> — needed wherever a role is only
    /// available as an already-minted claim value rather than the original
    /// <see cref="PlatformRole"/> enum, e.g. VerifyMfaCommandHandler resolving
    /// scopes (PlatformRoleScopes.ResolveScopes) from the role claims an MFA
    /// challenge carried over from Login.
    /// </summary>
    public static PlatformRole FromClaimValue(string claimValue) => claimValue switch
    {
        "customer" => PlatformRole.Customer,
        "support_agent" => PlatformRole.SupportAgent,
        "admin" => PlatformRole.Admin,
        "partner_api" => PlatformRole.PartnerApi,
        _ => throw new ArgumentOutOfRangeException(nameof(claimValue), claimValue, null)
    };
}
