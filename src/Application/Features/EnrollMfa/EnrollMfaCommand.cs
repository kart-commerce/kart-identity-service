using MediatR;

namespace Kart.Identity.Application.Features.EnrollMfa;

/// <summary>
/// api-contract.yaml POST /auth/mfa/enroll — no request body (bearerAuth only);
/// <see cref="UserId"/> is the caller's own id, resolved by the Api layer from the
/// validated access token's `sub` claim, never client-supplied. No Validator.cs
/// (nothing to validate) per this slice's shape (folder-structure.md).
/// </summary>
public sealed record EnrollMfaCommand(Guid UserId) : IRequest<EnrollMfaResponse>;
