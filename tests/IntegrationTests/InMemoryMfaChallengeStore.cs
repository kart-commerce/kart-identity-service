using System.Collections.Concurrent;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.IntegrationTests;

/// <summary>Test double for the Redis-backed <c>RedisMfaChallengeStore</c> — no Redis dependency needed for the test host.</summary>
public sealed class InMemoryMfaChallengeStore : IMfaChallengeStore
{
    private readonly ConcurrentDictionary<string, MfaChallengeState> _challenges = new();

    public Task<MfaChallenge> CreateAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var challengeId = Guid.NewGuid().ToString("N");
        _challenges[challengeId] = new MfaChallengeState(userId, roles);
        return Task.FromResult(new MfaChallenge(challengeId, 300));
    }

    public Task<MfaChallengeState?> GetAndConsumeAsync(string challengeId, CancellationToken cancellationToken) =>
        Task.FromResult(_challenges.TryRemove(challengeId, out var state) ? state : null);
}
