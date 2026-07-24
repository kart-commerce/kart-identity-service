namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// One-way password hashing (database-design.md `users.password_hash`) — not the
/// reversible AES-256 encryption a later ticket uses for the TOTP secret; these are
/// two different security properties (database-design.md's own explicit note).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
}
