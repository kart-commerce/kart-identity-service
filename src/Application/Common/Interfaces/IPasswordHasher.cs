namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// One-way password hashing (database-design.md `users.password_hash`) — not the
/// reversible AES-256 encryption a later ticket uses for the TOTP secret; these are
/// two different security properties (database-design.md's own explicit note).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies <paramref name="password"/> against <paramref name="hash"/>.
    /// <paramref name="hash"/> is null for an unknown account or a federated
    /// account that never set a native password — implementations must still pay
    /// an equivalent-cost dummy verification in that case (timing side-channel
    /// mitigation for account-existence probing at /auth/login) and return false.
    /// </summary>
    bool Verify(string password, string? hash);
}
