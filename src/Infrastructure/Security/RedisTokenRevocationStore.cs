using Kart.Identity.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// `identity:revocation:*` in the shared Redis deployment (design-decisions.md,
/// "Shared State-Store Technology for Ephemeral Security State") — the Gateway
/// reads this same key space on every request (architecture.md's Dependencies
/// table) to catch a revoked-but-unexpired token.
/// </summary>
public sealed class RedisTokenRevocationStore(IConnectionMultiplexer redis) : ITokenRevocationStore
{
    /// <summary>
    /// requirement-spec.md §4's ~15-minute access-token lifetime — any token that
    /// could still be live when a per-user marker is written has expired by the
    /// time this TTL elapses, so the marker never needs to outlive it.
    /// </summary>
    private static readonly TimeSpan UserMarkerTtl = TimeSpan.FromMinutes(15);

    public async Task RevokeTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(TokenKey(jti), "1", ttl);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(UserKey(userId), revokedAt.ToUnixTimeSeconds(), UserMarkerTtl);
    }

    private static string TokenKey(string jti) => $"identity:revocation:token:{jti}";
    private static string UserKey(Guid userId) => $"identity:revocation:user:{userId}";
}
