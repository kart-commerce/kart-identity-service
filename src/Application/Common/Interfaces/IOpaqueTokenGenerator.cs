namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Generates the raw, high-entropy opaque refresh-token value returned to the
/// client exactly once — only its hash (<see cref="ITokenHasher"/>) is ever
/// persisted (database-design.md `refresh_tokens.token_hash`).
/// </summary>
public interface IOpaqueTokenGenerator
{
    string Generate();
}
