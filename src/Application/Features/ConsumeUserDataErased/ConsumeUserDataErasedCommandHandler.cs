using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.ConsumeUserDataErased;

/// <summary>
/// event-contract.md `UserDataErased` — ADR-0016, ddd-model.md's `UserIdentity`
/// aggregate invariant, database-design.md's "Resolved — UserDataErased (ADR-0016)
/// Redaction Shape". On receipt: tombstones the `UserIdentity` aggregate's owned
/// PII (email/display name/password hash), deletes its `MfaCredential` and every
/// `FederatedIdentity` child, then revokes every live `Session` for the user —
/// the one operation in this bounded context that spans more than one aggregate
/// root, as sequential single-aggregate writes rather than one multi-aggregate
/// ACID transaction (ddd-model.md, "Cross-Aggregate Interaction"), mirroring the
/// same "enumerate live sessions, revoke each" shape `LockUserCommandHandler` and
/// `ConfirmPasswordResetCommandHandler` already use. Idempotent-safe for
/// at-least-once redelivery: an already-erased user has no MFA/federated rows or
/// live sessions left to touch, so a repeat delivery is a pure no-op.
/// </summary>
public sealed class ConsumeUserDataErasedCommandHandler(
    IIdentityDbContext dbContext,
    ITokenRevocationStore revocationStore,
    IDateTimeProvider dateTimeProvider,
    ILogger<ConsumeUserDataErasedCommandHandler> logger)
    : IRequestHandler<ConsumeUserDataErasedCommand>
{
    private const string ErasedBy = "system:user-service-erasure-consumer";

    public async Task Handle(ConsumeUserDataErasedCommand request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            logger.LogInformation(
                "UserDataErased received for user {UserId} with no matching account (already erased or never existed) — no-op",
                userId);
            return;
        }

        var now = dateTimeProvider.UtcNow;

        // Steps 1-3: single UserIdentity-aggregate transaction (database-design.md).
        user.Erase(now);

        var mfaCredential = await dbContext.MfaCredentials.SingleOrDefaultAsync(m => m.UserId == userId, cancellationToken);
        if (mfaCredential is not null)
        {
            dbContext.MfaCredentials.Remove(mfaCredential);
        }

        var federatedIdentities = await dbContext.FederatedIdentities
            .Where(f => f.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.FederatedIdentities.RemoveRange(federatedIdentities);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Step 4: per-Session writes, sequential rather than part of the transaction above.
        var liveSessions = await dbContext.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in liveSessions)
        {
            session.Revoke(SessionRevocationReason.Erasure, now, ErasedBy);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Same revocation-list write shape as LockUser/ConfirmPasswordReset — an
        // erased account's already-minted, still-unexpired tokens must never be
        // honored again (edge-cases.md, "UserDataErased Arrives While the User
        // Holds Active Sessions"), not just its future login attempts.
        await revocationStore.RevokeAllForUserAsync(userId.Value, now, cancellationToken);

        logger.LogInformation(
            "User data erased for user {UserId}, {SessionCount} sessions revoked",
            userId,
            liveSessions.Count);
    }
}
