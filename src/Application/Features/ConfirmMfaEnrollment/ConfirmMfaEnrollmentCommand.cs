using MediatR;

namespace Kart.Identity.Application.Features.ConfirmMfaEnrollment;

/// <summary>
/// api-contract.yaml POST /auth/mfa/enroll/confirm — <see cref="UserId"/> is the
/// caller's own id, resolved by the Api layer from the validated access token's
/// `sub` claim, never client-supplied (same as EnrollMfaCommand).
/// </summary>
public sealed record ConfirmMfaEnrollmentCommand(Guid UserId, string TotpCode) : IRequest;
