using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.RequestOtp;

/// <summary>
/// api-contract.yaml POST /auth/otp/request — always responds 202 regardless of
/// whether the email matches an account, same account-enumeration-avoidance shape as
/// InitiatePasswordResetCommandHandler. Unlike that handler (a flagged, un-fixed
/// delivery gap), this one closes the loop: it enqueues a real `OtpCodeRequested`
/// outbox event carrying the code itself for kart-notification-service to deliver.
/// </summary>
public sealed class RequestOtpCommandHandler(
    IIdentityDbContext dbContext,
    IOtpCodeStore otpCodeStore,
    IOtpAttemptThrottle otpAttemptThrottle,
    IDateTimeProvider dateTimeProvider,
    ILogger<RequestOtpCommandHandler> logger)
    : IRequestHandler<RequestOtpCommand>
{
    public async Task Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.From(request.Email.Trim());

        if (await otpAttemptThrottle.IsBlockedAsync(email.ToString(), request.IpAddress, cancellationToken))
        {
            logger.LogWarning("Stage {Stage}: OTP request rejected for {Email}, rate limit exceeded", "OtpRateLimitExceeded", email);
            throw new OtpRateLimitExceededException();
        }

        await otpAttemptThrottle.RecordAttemptAsync(email.ToString(), request.IpAddress, cancellationToken);

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            // Deliberate no-op, not a failure — api-contract.yaml always responds 202 regardless
            // of whether the email matches an account (account-enumeration avoidance), so this
            // has to be its own searchable Stage rather than silently falling through.
            logger.LogInformation("Stage {Stage}: OTP request no-op for {Email}, no matching account", "OtpRequestNoOpUnknownEmail", email);
            return;
        }

        var code = await otpCodeStore.IssueAsync(email.ToString(), user.UserId.Value, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var otpRequested = OutboxEvent.Create(
            user.UserId.Value,
            "OtpCodeRequested",
            JsonSerializer.Serialize(new { userId = user.UserId.Value, email = user.Email?.Value, code, expiresInSeconds = 300 }),
            now,
            createdBy: "system:otp-request");

        dbContext.OutboxEvents.Add(otpRequested);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: OTP code issued for user {UserId}, outbox event {OtpRequestedEventId} (OtpCodeRequested) enqueued",
            "OtpCodeIssued",
            user.UserId,
            otpRequested.EventId);
    }
}
