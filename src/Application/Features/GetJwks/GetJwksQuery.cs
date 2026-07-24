using MediatR;

namespace Kart.Identity.Application.Features.GetJwks;

/// <summary>
/// api-contract.yaml GET /.well-known/jwks.json — no request parameters, so no
/// Validator.cs (nothing to validate) per this slice's shape (folder-structure.md).
/// </summary>
public sealed record GetJwksQuery : IRequest<GetJwksResponse>;
