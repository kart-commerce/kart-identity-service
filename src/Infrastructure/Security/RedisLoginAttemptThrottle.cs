using Kart.Identity.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Per-account and per-IP progressive throttling on /auth/login (edge-cases.md,
/// "Credential Stuffing / Brute-Force"), in the shared ephemeral-security-state
/// Redis deployment design-decisions.md specifies (`identity:login-throttle:*`).
/// "Progressive" here means: exceeding a fixed attempt threshold within a rolling
/// window trips a lockout whose duration doubles with each repeat offense inside a
/// longer violation window, up to a cap — an explicit engineering default, since
/// neither requirement-spec.md nor edge-cases.md name concrete thresholds.
/// </summary>
public sealed class RedisLoginAttemptThrottle(IConnectionMultiplexer redis) : ILoginAttemptThrottle
{
    private const int AttemptThreshold = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BaseLockout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxLockout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ViolationWindow = TimeSpan.FromHours(24);

    public async Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var emailBlocked = await db.KeyExistsAsync(BlockedKey("account", Normalize(email)));
        var ipBlocked = await db.KeyExistsAsync(BlockedKey("ip", ipAddress));
        return emailBlocked || ipBlocked;
    }

    public async Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await RecordFailureForScopeAsync(db, "account", Normalize(email));
        await RecordFailureForScopeAsync(db, "ip", ipAddress);
    }

    public async Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await Task.WhenAll(
            db.KeyDeleteAsync(AttemptsKey("account", Normalize(email))),
            db.KeyDeleteAsync(BlockedKey("account", Normalize(email))),
            db.KeyDeleteAsync(AttemptsKey("ip", ipAddress)),
            db.KeyDeleteAsync(BlockedKey("ip", ipAddress)));
    }

    private static async Task RecordFailureForScopeAsync(IDatabase db, string scope, string key)
    {
        var attemptsKey = AttemptsKey(scope, key);
        var attempts = await db.StringIncrementAsync(attemptsKey);
        if (attempts == 1)
        {
            await db.KeyExpireAsync(attemptsKey, AttemptWindow);
        }

        if (attempts < AttemptThreshold)
        {
            return;
        }

        var violationsKey = ViolationsKey(scope, key);
        var violations = await db.StringIncrementAsync(violationsKey);
        if (violations == 1)
        {
            await db.KeyExpireAsync(violationsKey, ViolationWindow);
        }

        var lockoutSeconds = Math.Min(BaseLockout.TotalSeconds * Math.Pow(2, violations - 1), MaxLockout.TotalSeconds);
        await db.StringSetAsync(BlockedKey(scope, key), "1", TimeSpan.FromSeconds(lockoutSeconds));
        await db.KeyDeleteAsync(attemptsKey);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string AttemptsKey(string scope, string key) => $"identity:login-throttle:attempts:{scope}:{key}";
    private static string ViolationsKey(string scope, string key) => $"identity:login-throttle:violations:{scope}:{key}";
    private static string BlockedKey(string scope, string key) => $"identity:login-throttle:blocked-until:{scope}:{key}";
}
