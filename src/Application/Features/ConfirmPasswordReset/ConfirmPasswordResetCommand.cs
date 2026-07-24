using MediatR;

namespace Kart.Identity.Application.Features.ConfirmPasswordReset;

/// <summary>api-contract.yaml POST /auth/password/reset-confirm.</summary>
public sealed record ConfirmPasswordResetCommand(string ResetToken, string NewPassword) : IRequest;
