using Kart.Identity.Application.Common;
using Kart.Identity.Application.Features.SocialLoginCallback;
using Kart.Identity.Application.Features.StartSocialLogin;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1/auth/sso/social/*` paths — customer social login.</summary>
public static class SocialSsoEndpoints
{
    public static IEndpointRouteBuilder MapSocialSsoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/auth/sso/social/{provider}/login", async (string provider, ISender sender, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
            logger.LogInformation("Stage {Stage}: social login start requested for provider {Provider}", "SocialLoginStartRequestReceived", provider);
            var redirectUrl = await sender.Send(new StartSocialLoginQuery(provider), cancellationToken);
            logger.LogInformation("Stage {Stage}: social login redirect issued for provider {Provider}", "SocialLoginRedirectIssued", provider);
            return Results.Redirect(redirectUrl);
        })
        .WithName("StartSocialLogin")
        .Produces(StatusCodes.Status302Found)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/v1/auth/sso/social/{provider}/callback", async (string provider, string code, string state, ISender sender, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
            logger.LogInformation("Stage {Stage}: social login callback received for provider {Provider}", "SocialLoginCallbackReceived", provider);
            var response = await sender.Send(new SocialLoginCallbackCommand(provider, code, state), cancellationToken);
            logger.LogInformation("Stage {Stage}: social login process completed successfully for provider {Provider}", "SocialLoginProcessCompletedSuccessfully", provider);
            return Results.Ok(response);
        })
        .WithName("SocialLoginCallback")
        .Produces<SocialLoginCallbackResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
