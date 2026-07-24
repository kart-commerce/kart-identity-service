using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// The `MfaCredential` child entity of the `UserIdentity` aggregate (ddd-model.md)
/// — database-design.md `mfa_credentials`. One active-or-pending TOTP credential
/// per user (`user_id` is the table's own primary key); enrolling again replaces
/// the row rather than adding a second one.
/// </summary>
public sealed class MfaCredential
{
    public Guid UserId { get; private set; }
    public byte[] EncryptedSecret { get; private set; } = [];
    public MfaCredentialStatus Status { get; private set; }
    public DateTimeOffset EnrolledAt { get; private set; }
    public DateTimeOffset? PendingExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private MfaCredential()
    {
    }

    /// <summary>api-contract.yaml POST /auth/mfa/enroll — first-ever enrollment for this user.</summary>
    public static MfaCredential BeginEnrollment(Guid userId, byte[] encryptedSecret, DateTimeOffset now, TimeSpan pendingWindow)
    {
        var credential = new MfaCredential { UserId = userId, CreatedBy = userId.ToString() };
        credential.RestartEnrollment(encryptedSecret, now, pendingWindow);
        return credential;
    }

    /// <summary>
    /// database-design.md: "enrolling again replaces the row" — re-enrolling over
    /// an existing pending *or already-active* credential restarts the pending
    /// window from a freshly generated secret; the prior credential (confirmed or
    /// not) is no longer valid the instant this is called.
    /// </summary>
    public void RestartEnrollment(byte[] encryptedSecret, DateTimeOffset now, TimeSpan pendingWindow)
    {
        EncryptedSecret = encryptedSecret;
        Status = MfaCredentialStatus.Pending;
        EnrolledAt = now;
        PendingExpiresAt = now.Add(pendingWindow);
        ConfirmedAt = null;
        UpdatedAt = now;
        UpdatedBy = UserId.ToString();
    }
}
