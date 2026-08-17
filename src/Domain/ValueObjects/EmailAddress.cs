using System.Text.RegularExpressions;

namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// A validated, normalized email address — database-design.md `users.email` (a case-insensitive
/// `citext` column, unique when present). This type protects its own format invariant only;
/// <see cref="Entities.User"/> owns the entity-level rule of *when* an email may be absent
/// (native accounts always have one, federated accounts may not — api-contract.yaml,
/// edge-cases.md), which is a `User` business rule, not part of what makes a string a valid
/// email address.
/// </summary>
public readonly partial record struct EmailAddress
{
    private const int MaxLength = 320;

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null/blank, exceeds <see cref="MaxLength"/>, or is not in a
    /// valid `local@domain` shape.
    /// </exception>
    public static EmailAddress From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength || !FormatRegex().IsMatch(trimmed))
        {
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
        }

        return new EmailAddress(trimmed.ToLowerInvariant());
    }

    /// <summary>Non-throwing form for call sites that need to validate untrusted input first.</summary>
    public static bool TryCreate(string? value, out EmailAddress email)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimmed = value.Trim();
            if (trimmed.Length <= MaxLength && FormatRegex().IsMatch(trimmed))
            {
                email = new EmailAddress(trimmed.ToLowerInvariant());
                return true;
            }
        }

        email = default;
        return false;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatRegex();
}
