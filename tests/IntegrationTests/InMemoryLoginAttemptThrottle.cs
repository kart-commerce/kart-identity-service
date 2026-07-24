using System.Collections.Concurrent;
using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Test double for the Redis-backed <c>RedisLoginAttemptThrottle</c> — real
/// threshold-based blocking behavior without a Redis dependency, so
/// LoginEndpointTests can exercise the 429 path over real HTTP.
/// </summary>
public sealed class InMemoryLoginAttemptThrottle : ILoginAttemptThrottle
{
    private const int AttemptThreshold = 5;
    private readonly ConcurrentDictionary<string, int> _attempts = new();
    private readonly ConcurrentDictionary<string, bool> _blocked = new();

    public Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken) =>
        Task.FromResult(_blocked.ContainsKey(Key("account", email)) || _blocked.ContainsKey(Key("ip", ipAddress)));

    public Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken)
    {
        RecordFailure(Key("account", email));
        RecordFailure(Key("ip", ipAddress));
        return Task.CompletedTask;
    }

    public Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken)
    {
        _attempts.TryRemove(Key("account", email), out _);
        _blocked.TryRemove(Key("account", email), out _);
        _attempts.TryRemove(Key("ip", ipAddress), out _);
        _blocked.TryRemove(Key("ip", ipAddress), out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Test-only hook: this instance is a singleton for the whole
    /// <see cref="IdentityApiFactory"/> class fixture, and every in-process
    /// TestServer request shares one synthetic remote IP — without a reset, one
    /// test tripping the IP-based block would leak into every other test sharing
    /// the fixture. Call at the start of each test that needs a clean slate.
    /// </summary>
    public void ClearAll()
    {
        _attempts.Clear();
        _blocked.Clear();
    }

    private void RecordFailure(string key)
    {
        var count = _attempts.AddOrUpdate(key, 1, (_, existing) => existing + 1);
        if (count >= AttemptThreshold)
        {
            _blocked[key] = true;
        }
    }

    private static string Key(string scope, string value) => $"{scope}:{value.Trim().ToLowerInvariant()}";
}
