using MediatR;

namespace Kart.Identity.Application.Features.InitiatePasswordReset;

/// <summary>api-contract.yaml POST /auth/password/reset-initiate.</summary>
public sealed record InitiatePasswordResetCommand(string Email) : IRequest;
