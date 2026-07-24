using System.Collections.Concurrent;
using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.IntegrationTests;

/// <summary>Test double for the Redis-backed <c>RedisSamlAssertionReplayStore</c> — no Redis dependency needed for the test host.</summary>
public sealed class InMemorySamlAssertionReplayStore : ISamlAssertionReplayStore
{
    private readonly ConcurrentDictionary<string, byte> _consumed = new();

    public Task<bool> TryConsumeAsync(string assertionId, TimeSpan ttl, CancellationToken cancellationToken) =>
        Task.FromResult(_consumed.TryAdd(assertionId, 1));
}
