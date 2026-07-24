using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>One-way password hashing (database-design.md `users.password_hash`: "bcrypt/argon2id"). bcrypt chosen — mature, no native library dependency.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// A validly-formatted but unreachable hash, verified against whenever there's
    /// no real one — keeps /auth/login's timing cost equal for "no such account"
    /// and "wrong password" (account-existence timing side-channel mitigation).
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString(), workFactor: 12);

    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);

    public bool Verify(string password, string? hash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, hash ?? DummyHash) && hash is not null;
}
