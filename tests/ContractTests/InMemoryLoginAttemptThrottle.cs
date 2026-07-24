using Kart.Identity.Application.Common.Interfaces;

namespace Kart.Identity.ContractTests;

/// <summary>Test double for the Redis-backed <c>RedisLoginAttemptThrottle</c> — never blocks, since contract tests exercise shape, not throttling behavior.</summary>
public sealed class InMemoryLoginAttemptThrottle : ILoginAttemptThrottle
{
    public Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken) => Task.CompletedTask;
}
