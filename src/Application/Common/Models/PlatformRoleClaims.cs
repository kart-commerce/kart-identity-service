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
}
