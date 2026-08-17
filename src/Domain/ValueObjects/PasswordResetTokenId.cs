namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a `PasswordResetToken` — database-design.md
/// `password_reset_tokens.reset_token_id`.
/// </summary>
public readonly record struct PasswordResetTokenId(Guid Value) : ITypedEntityId<PasswordResetTokenId>
{
    public static PasswordResetTokenId New() => new(Guid.NewGuid());

    public static PasswordResetTokenId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
