using Kart.Identity.Application.Features.SocialLoginCallback;
using Kart.Identity.Application.Features.StartSocialLogin;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1/auth/sso/social/*` paths — customer social login.</summary>
public static class SocialSsoEndpoints
{
    public static IEndpointRouteBuilder MapSocialSsoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/auth/sso/social/{provider}/login", async (string provider, ISender sender, CancellationToken cancellationToken) =>
        {
            var redirectUrl = await sender.Send(new StartSocialLoginQuery(provider), cancellationToken);
            return Results.Redirect(redirectUrl);
        })
        .WithName("StartSocialLogin")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/v1/auth/sso/social/{provider}/callback", async (string provider, string code, string state, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new SocialLoginCallbackCommand(provider, code, state), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("SocialLoginCallback")
        .Produces<SocialLoginCallbackResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
