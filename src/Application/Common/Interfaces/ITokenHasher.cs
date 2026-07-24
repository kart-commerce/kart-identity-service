namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Hashes an already-high-entropy opaque secret (refresh token, password-reset
/// token) for storage/lookup — deliberately not the same slow, salted algorithm as
/// <see cref="IPasswordHasher"/>, since the input here is already random rather
/// than user-chosen (database-design.md's `refresh_tokens.token_hash` note).
/// </summary>
public interface ITokenHasher
{
    string Hash(string rawToken);
}
