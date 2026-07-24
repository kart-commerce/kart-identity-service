using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kart.Identity.Application.Features.EnrollMfa;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1/auth/mfa/*` paths — bearer-authenticated.</summary>
public static class MfaEndpoints
{
    public static IEndpointRouteBuilder MapMfaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/mfa/enroll", async (HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var response = await sender.Send(new EnrollMfaCommand(userId), cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("EnrollMfa")
        .Produces<EnrollMfaResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
