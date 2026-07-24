using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `idp_group_role_mappings` — the config-driven authority
/// requirement-spec.md §2 (resolved Open Question #7) establishes. Identified by
/// `(IdpAlias, ExternalGroupClaim)`. Written by an out-of-band operator process;
/// no public endpoint provisions these rows in v1 (same flagged gap as
/// `ServicePrincipal` provisioning) — <see cref="Create"/> exists for that future
/// operator-tooling/seed path, not for IDN-14/IDN-15 themselves, which only ever
/// read this table.
/// </summary>
public sealed class IdpGroupRoleMapping
{
    public Guid MappingId { get; private set; }
    public string IdpAlias { get; private set; } = string.Empty;
    public string ExternalGroupClaim { get; private set; } = string.Empty;
    public PlatformRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private IdpGroupRoleMapping()
    {
    }

    /// <summary>database-design.md: only `SupportAgent` or `Admin` are valid federation-mapped targets.</summary>
    public static IdpGroupRoleMapping Create(string idpAlias, string externalGroupClaim, PlatformRole role, DateTimeOffset now, string createdBy)
    {
        if (role != PlatformRole.SupportAgent && role != PlatformRole.Admin)
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "idp_group_role_mappings.role only allows SupportAgent or Admin.");
        }

        return new IdpGroupRoleMapping
        {
            MappingId = Guid.NewGuid(),
            IdpAlias = idpAlias,
            ExternalGroupClaim = externalGroupClaim,
            Role = role,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy
        };
    }
}
