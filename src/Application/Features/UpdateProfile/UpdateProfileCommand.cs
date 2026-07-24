using MediatR;

namespace Kart.Identity.Application.Features.UpdateProfile;

/// <summary>api-contract.yaml PATCH /auth/profile.</summary>
public sealed record UpdateProfileCommand(Guid UserId, string? Email, string? DisplayName)
    : IRequest<UpdateProfileResponse>;
