using Kart.Identity.Application.Features.EnterpriseSamlAssertionConsumer;
using Kart.Identity.Application.Features.StartEnterpriseFederation;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1/auth/sso/enterprise/*` paths.</summary>
public static class EnterpriseSsoEndpoints
{
    public static IEndpointRouteBuilder MapEnterpriseSsoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/auth/sso/enterprise/{idpAlias}/login", async (string idpAlias, ISender sender, CancellationToken cancellationToken) =>
        {
            var redirectUrl = await sender.Send(new StartEnterpriseFederationQuery(idpAlias), cancellationToken);
            return Results.Redirect(redirectUrl);
        })
        .WithName("StartEnterpriseFederation")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/v1/auth/sso/enterprise/{idpAlias}/saml/acs", async (string idpAlias, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var samlResponse = form["SAMLResponse"].ToString();
            var response = await sender.Send(new EnterpriseSamlAssertionConsumerCommand(idpAlias, samlResponse), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("EnterpriseSamlAssertionConsumer")
        .Accepts<IFormCollection>("application/x-www-form-urlencoded")
        .Produces<EnterpriseSamlAssertionConsumerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
