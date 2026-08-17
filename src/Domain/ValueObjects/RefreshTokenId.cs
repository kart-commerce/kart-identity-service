namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a `RefreshToken` in the `Session` aggregate's rotation chain
/// (ddd-model.md) — database-design.md `refresh_tokens.token_id`. Also used for
/// `parent_token_id`/`replaced_by_token_id`, which reference this same identifier space.
/// </summary>
public readonly record struct RefreshTokenId(Guid Value) : ITypedEntityId<RefreshTokenId>
{
    public static RefreshTokenId New() => new(Guid.NewGuid());

    public static RefreshTokenId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
