namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Server-side-only passwordless-login one-time-code state (User Registration, Login &amp;
/// Authentication Journey — "Email/Phone → OTP/Password" step), same shared
/// ephemeral-security-state Redis deployment as IMfaChallengeStore (`identity:otp-code:*`).
/// A code is single-use: a successful <see cref="VerifyAndConsumeAsync"/> deletes it, so
/// replaying a spent code fails exactly like an unknown one.
/// </summary>
public interface IOtpCodeStore
{
    /// <summary>Generates and stores a fresh code for this email, overwriting any still-pending one. Returns the raw code — never persisted anywhere in plaintext, only handed to the caller to embed in the delivery event.</summary>
    Task<string> IssueAsync(string email, Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the associated userId and deletes the code on a match; returns null (without deleting) on a wrong/unknown/expired code, so a mistyped digit doesn't burn the user's only valid code.</summary>
    Task<Guid?> VerifyAndConsumeAsync(string email, string code, CancellationToken cancellationToken);
}
