using System.Collections.Concurrent;
using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.IntegrationTests;

/// <summary>Test double for the Redis-backed <c>RedisTokenRevocationStore</c> — no Redis dependency needed for the test host.</summary>
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, byte> _revokedJtis = new();

    public Task RevokeTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
    {
        _revokedJtis[jti] = 1;
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken) => Task.CompletedTask;
}
