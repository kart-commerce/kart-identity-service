using MediatR;

namespace Kart.Identity.Application.Features.VerifyMfa;

/// <summary>api-contract.yaml POST /auth/mfa/verify — completes a pending Login MFA challenge (IDN-3) and mints tokens.</summary>
public sealed record VerifyMfaCommand(string ChallengeId, string TotpCode) : IRequest<VerifyMfaResponse>;
