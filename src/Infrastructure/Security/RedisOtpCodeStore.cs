using System.Security.Cryptography;
using System.Text.Json;
using Kart.Identity.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Server-side-only passwordless-login one-time code — `identity:otp-code:*` in the
/// shared ephemeral-security-state Redis deployment (design-decisions.md), same
/// pattern as RedisMfaChallengeStore. 5-minute TTL and 6 numeric digits are explicit
/// engineering defaults (no design doc names concrete values for this new-to-this-flow
/// feature). The code itself is hashed at rest via ITokenHasher, mirroring how
/// PasswordResetToken/RefreshToken never store their raw secret either — the hash
/// doesn't add meaningful offline-crack resistance over a 6-digit space, but keeps this
/// store consistent with every other secret-at-rest in this service.
/// </summary>
public sealed class RedisOtpCodeStore(IConnectionMultiplexer redis, ITokenHasher tokenHasher) : IOtpCodeStore
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);

    public async Task<string> IssueAsync(string email, Guid userId, CancellationToken cancellationToken)
    {
        var code = GenerateSixDigitCode();
        var payload = JsonSerializer.Serialize(new StoredCode(userId, tokenHasher.Hash(code)));

        var db = redis.GetDatabase();
        await db.StringSetAsync(CodeKey(email), payload, CodeTtl);

        return code;
    }

    public async Task<Guid?> VerifyAndConsumeAsync(string email, string code, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = CodeKey(email);
        var payload = await db.StringGetAsync(key);
        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredCode>(payload!)!;
        if (stored.CodeHash != tokenHasher.Hash(code))
        {
            // Wrong digit(s) — leave the still-valid code in place so a mistyped
            // attempt doesn't burn the user's only code before the TTL expires;
            // IOtpAttemptThrottle is what actually caps repeated wrong guesses.
            return null;
        }

        await db.KeyDeleteAsync(key);
        return stored.UserId;
    }

    private static string GenerateSixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string CodeKey(string email) => $"identity:otp-code:{email.Trim().ToLowerInvariant()}";

    private sealed record StoredCode(Guid UserId, string CodeHash);
}
