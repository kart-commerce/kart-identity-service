using System.Security.Cryptography;
using System.Text;
using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Fast, deterministic hash for already-high-entropy opaque tokens
/// (database-design.md `refresh_tokens.token_hash`) — no salt/slow-KDF needed since,
/// unlike a password, the input isn't user-chosen/low-entropy.
/// </summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
