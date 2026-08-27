using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.ValueObjects;
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
        var userId = UserId.From(request.UserId);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new UserNotFoundException();

        var rawEmail = request.Email?.Trim();
        var email = rawEmail is not null ? EmailAddress.From(rawEmail) : (EmailAddress?)null;
        if (email is not null)
        {
            var emailTaken = await dbContext.Users.AnyAsync(u => u.UserId != userId && u.Email == email, cancellationToken);
            if (emailTaken)
            {
                throw new EmailAlreadyRegisteredException(rawEmail!);
            }
        }

        var now = dateTimeProvider.UtcNow;
        user.UpdateProfile(email, request.DisplayName, now);

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            user.UserId.Value,
            "UserAccountUpdated",
            JsonSerializer.Serialize(new { userId = user.UserId.Value, email = user.Email?.Value, displayName = user.DisplayName, updatedAt = now }),
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
            throw new EmailAlreadyRegisteredException(rawEmail ?? string.Empty);
        }

        // Never the email/display name themselves — those are the PII this
        // update mutates, not something to echo into a log line.
        logger.LogInformation("Stage {Stage}: profile persisted and UserAccountUpdated outbox event saved for user {UserId}", "ProfilePersisted", user.UserId);

        return new UpdateProfileResponse(user.Email?.Value, user.DisplayName, now);
    }
}
