namespace Kart.Identity.Domain.Enums;

/// <summary>
/// requirement-spec.md §2's four-role platform vocabulary (BRD §24.1). Identity is
/// the platform's single issuer/resolver of this vocabulary (Domain Invariant §4).
/// </summary>
public enum PlatformRole
{
    Customer,
    SupportAgent,
    Admin,
    PartnerApi
}
