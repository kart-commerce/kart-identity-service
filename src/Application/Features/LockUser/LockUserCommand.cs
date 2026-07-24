using MediatR;

namespace Kart.Identity.Application.Features.LockUser;

/// <summary>
/// api-contract.yaml POST /internal/users/{userId}/lock. <paramref name="LockedBy"/>
/// is the calling service principal's client_id, read off the bearer token's `sub`
/// claim by the endpoint, not supplied by the caller.
/// </summary>
public sealed record LockUserCommand(string UserId, string LockedBy) : IRequest;
