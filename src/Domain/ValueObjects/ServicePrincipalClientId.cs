namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// The business-assigned identifier a <see cref="Entities.ServicePrincipal"/> is looked up by —
/// database-design.md `service_principals.client_id`, this table's own primary key (a
/// human-assigned identifier, never a generated Guid, unlike every other aggregate root in this
/// service). Distinct from the various external OAuth `client_id`s Infrastructure's federation
/// config holds for enterprise/social IdPs (<c>EnterpriseIdpConfig</c>, <c>SocialIdpConfig</c>) —
/// those identify an *external* system's client and are plain strings by design; this type is
/// only for our own `service_principals` row key.
/// </summary>
public readonly record struct ServicePrincipalClientId
{
    private const int MaxLength = 100;

    public string Value { get; }

    private ServicePrincipalClientId(string value)
    {
        Value = value;
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null/blank or exceeds <see cref="MaxLength"/> characters.
    /// </exception>
    public static ServicePrincipalClientId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Client id must be at most {MaxLength} characters.", nameof(value));
        }

        return new ServicePrincipalClientId(trimmed);
    }

    public override string ToString() => Value;
}
