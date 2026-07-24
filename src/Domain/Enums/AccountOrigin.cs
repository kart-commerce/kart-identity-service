namespace Kart.Identity.Domain.Enums;

/// <summary>
/// database-design.md `users.account_origin` — denormalized for observability/audit
/// only; source of truth for provenance is the `FederatedIdentity` child collection
/// (a later ticket) when not <see cref="Native"/>.
/// </summary>
public enum AccountOrigin
{
    Native,
    Social,
    Enterprise
}
