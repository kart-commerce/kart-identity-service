using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.IntegrationTests;

/// <summary>Test double for the Redis-backed <c>RedisMfaChallengeStore</c> — no Redis dependency needed for the test host.</summary>
public sealed class InMemoryMfaChallengeStore : IMfaChallengeStore
{
    public Task<MfaChallenge> CreateAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken) =>
        Task.FromResult(new MfaChallenge(Guid.NewGuid().ToString("N"), 300));
}
