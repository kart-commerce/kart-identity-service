using MediatR;

namespace Kart.Identity.Application.Features.RegisterUser;

/// <summary>api-contract.yaml POST /auth/register.</summary>
public sealed record RegisterUserCommand(string Email, string Password, string? DisplayName)
    : IRequest<RegisterUserResponse>;
