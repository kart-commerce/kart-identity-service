using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.UnlockUser;

/// <summary>
/// api-contract.yaml POST /internal/users/{userId}/unlock — same caller
/// restriction as LockUser (ADR-0010). Restores the user's ability to
/// authenticate; does not restore sessions revoked by the original lock — only
/// future login attempts are re-permitted, exactly as ordinary logout/reuse
/// revocation is never implicitly undone either.
/// </summary>
public sealed class UnlockUserCommandHandler(
    IIdentityDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<UnlockUserCommandHandler> logger)
    : IRequestHandler<UnlockUserCommand>
{
    public async Task Handle(UnlockUserCommand request, CancellationToken cancellationToken)
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

        user.Unlock(dateTimeProvider.UtcNow, request.UnlockedBy);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} unlocked by {UnlockedBy}", userId, request.UnlockedBy);
    }
}
