using Kart.Identity.Application.Features.GetJwks;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>
/// api-contract.yaml GET /.well-known/jwks.json — deliberately unversioned
/// (IANA well-known discovery path convention, not a versioned business
/// endpoint) and unauthenticated (the Gateway's only Identity coupling,
/// cached and infrequent per architecture.md).
/// </summary>
public static class JwksEndpoints
{
    public static IEndpointRouteBuilder MapJwksEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/jwks.json", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new GetJwksQuery(), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetJwks")
        .Produces<GetJwksResponse>(StatusCodes.Status200OK);

        return app;
    }
}
