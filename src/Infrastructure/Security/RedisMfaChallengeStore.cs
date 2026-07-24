using System.Text.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Server-side-only MFA partial-auth challenge (edge-cases.md, "Partial-Auth
/// Window During MFA") — `identity:mfa-challenge:*` in the shared Redis deployment
/// (design-decisions.md). 5-minute TTL is an explicit engineering default; neither
/// requirement-spec.md nor edge-cases.md name a concrete window.
/// </summary>
public sealed class RedisMfaChallengeStore(IConnectionMultiplexer redis, IOpaqueTokenGenerator opaqueTokenGenerator)
    : IMfaChallengeStore
{
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);

    public async Task<MfaChallenge> CreateAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var challengeId = opaqueTokenGenerator.Generate();
        var payload = JsonSerializer.Serialize(new ChallengePayload(userId, roles));

        var db = redis.GetDatabase();
        await db.StringSetAsync(ChallengeKey(challengeId), payload, ChallengeTtl);

        return new MfaChallenge(challengeId, (int)ChallengeTtl.TotalSeconds);
    }

    public async Task<MfaChallengeState?> GetAndConsumeAsync(string challengeId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var payload = await db.StringGetDeleteAsync(ChallengeKey(challengeId));
        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        var parsed = JsonSerializer.Deserialize<ChallengePayload>(payload!)!;
        return new MfaChallengeState(parsed.UserId, parsed.Roles);
    }

    private static string ChallengeKey(string challengeId) => $"identity:mfa-challenge:{challengeId}";

    private sealed record ChallengePayload(Guid UserId, IReadOnlyCollection<string> Roles);
}
