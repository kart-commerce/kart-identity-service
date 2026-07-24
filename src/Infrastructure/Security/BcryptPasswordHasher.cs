using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>One-way password hashing (database-design.md `users.password_hash`: "bcrypt/argon2id"). bcrypt chosen — mature, no native library dependency.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);
}
