using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `service_principals` — the OAuth2 Client Credentials
/// counterpart to `User`. `client_id` is the table's own primary key (a
/// human-assigned identifier, not a generated Guid, unlike every other
/// aggregate root in this service). No public endpoint provisions these rows
/// (same out-of-band gap tickets.md flags for native role elevation) — this
/// factory exists for that future operator-tooling/seed path, not for IDN-7
/// itself, which only ever reads this table.
/// </summary>
public sealed class ServicePrincipal
{
    public string ClientId { get; private set; } = string.Empty;
    public string ClientSecretHash { get; private set; } = string.Empty;
    public PlatformRole Role { get; private set; }
    public ServicePrincipalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private ServicePrincipal()
    {
    }

    /// <summary>database-design.md: only `Admin` or `PartnerApi` are valid roles for a non-interactive principal.</summary>
    public static ServicePrincipal Provision(string clientId, string clientSecretHash, PlatformRole role, DateTimeOffset now, string createdBy)
    {
        if (role != PlatformRole.Admin && role != PlatformRole.PartnerApi)
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "service_principals.role only allows Admin or PartnerApi.");
        }

        return new ServicePrincipal
        {
            ClientId = clientId,
            ClientSecretHash = clientSecretHash,
            Role = role,
            Status = ServicePrincipalStatus.Active,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy
        };
    }
}
