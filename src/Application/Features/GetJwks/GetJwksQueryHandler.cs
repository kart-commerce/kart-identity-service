using Kart.Identity.Application.Common.Interfaces;
using MediatR;

namespace Kart.Identity.Application.Features.GetJwks;

public sealed class GetJwksQueryHandler(IJwtKeyProvider keyProvider)
    : IRequestHandler<GetJwksQuery, GetJwksResponse>
{
    public Task<GetJwksResponse> Handle(GetJwksQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new GetJwksResponse(keyProvider.GetPublicSigningKeys()));
}
