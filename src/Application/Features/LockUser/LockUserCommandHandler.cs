using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.LockUser;

/// <summary>
/// api-contract.yaml POST /internal/users/{userId}/lock — ADR-0010; requirement-spec.md
/// §2's admin-suspension FR. Locks native/social/enterprise authentication for this
/// user and revokes every one of their live sessions (database-design.md
/// `idx_sessions_user_live`), then writes the per-user revocation-list marker
/// (edge-cases.md, "RBAC Role Change Outlives an Already-Minted JWT" — the same
/// mechanism, reused here for admin-lock) so already-minted, still-unexpired access
/// tokens are rejected by the Gateway too, not just future login attempts.
/// </summary>
public sealed class LockUserCommandHandler(
    IIdentityDbContext dbContext,
    ITokenRevocationStore revocationStore,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LockUserCommand>
{
    public async Task Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new UserNotFoundException();
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            throw new UserNotFoundException();
        }

        var now = dateTimeProvider.UtcNow;
        user.Lock(now, request.LockedBy);

        var liveSessions = await dbContext.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in liveSessions)
        {
            session.Revoke(SessionRevocationReason.AdminLock, now, request.LockedBy);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await revocationStore.RevokeAllForUserAsync(userId, now, cancellationToken);
    }
}
