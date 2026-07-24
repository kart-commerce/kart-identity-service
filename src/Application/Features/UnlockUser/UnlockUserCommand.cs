using MediatR;

namespace Kart.Identity.Application.Features.UnlockUser;

/// <summary>
/// api-contract.yaml POST /internal/users/{userId}/unlock. <paramref name="UnlockedBy"/>
/// is the calling service principal's client_id, read off the bearer token's `sub`
/// claim by the endpoint, not supplied by the caller.
/// </summary>
public sealed record UnlockUserCommand(string UserId, string UnlockedBy) : IRequest;
