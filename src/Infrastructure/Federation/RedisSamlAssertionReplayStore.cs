using Kart.Identity.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// `identity:saml-assertion:*` in the shared Redis deployment (design-decisions.md,
/// "Shared State-Store Technology for Ephemeral Security State") — edge-cases.md's
/// "SAML Assertion Replay at the ACS Endpoint" consumed-assertion-ID cache.
/// </summary>
public sealed class RedisSamlAssertionReplayStore(IConnectionMultiplexer redis) : ISamlAssertionReplayStore
{
    public async Task<bool> TryConsumeAsync(string assertionId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        // SET ... NX is the atomic check-and-mark this needs — two concurrent
        // submissions of the same replayed assertion must not both "win".
        return await db.StringSetAsync(AssertionKey(assertionId), "1", ttl, When.NotExists);
    }

    private static string AssertionKey(string assertionId) => $"identity:saml-assertion:{assertionId}";
}
