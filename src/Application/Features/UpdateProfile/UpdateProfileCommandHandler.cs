using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.UpdateProfile;

/// <summary>
/// api-contract.yaml PATCH /auth/profile — updates the authenticated user's login
/// email and/or display name, publishing `UserAccountUpdated` (payload: userId,
/// email, displayName, updatedAt) onto this service's own `identity.exchange`
/// (design-decisions.md, "Event Publication Reliability"; requirement-spec.md
/// §2/§4, ADR-0006). `updatedAt` is the monotonic-per-user ordering field
/// event-contract.md's "Out-of-Order Delivery of Successive UserAccountUpdated
/// Events" decision requires — User Service applies last-write-wins by this
/// value rather than by delivery order.
/// </summary>
public sealed class UpdateProfileCommandHandler(
    IIdentityDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateProfileCommandHandler> logger)
    : IRequestHandler<UpdateProfileCommand, UpdateProfileResponse>
{
    public async Task<UpdateProfileResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        var email = request.Email?.Trim();
        if (email is not null)
        {
            var emailTaken = await dbContext.Users.AnyAsync(u => u.UserId != request.UserId && u.Email == email, cancellationToken);
            if (emailTaken)
            {
                throw new EmailAlreadyRegisteredException(email);
            }
        }

        var now = dateTimeProvider.UtcNow;
        user.UpdateProfile(email, request.DisplayName, now);

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            user.UserId,
            "UserAccountUpdated",
            JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email, displayName = user.DisplayName, updatedAt = now }),
            now,
            createdBy: user.UserId.ToString()));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Only uq_users_email can plausibly fail here — a concurrent
            // registration/profile-update claimed this email between our check and
            // this write (same race RegisterUserCommandHandler already closes).
            throw new EmailAlreadyRegisteredException(email ?? string.Empty);
        }

        // Never the email/display name themselves — those are the PII this
        // update mutates, not something to echo into a log line.
        logger.LogInformation("Profile updated for user {UserId}", user.UserId);

        return new UpdateProfileResponse(user.Email, user.DisplayName, now);
    }
}
