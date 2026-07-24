using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kart.Identity.Application.Features.LockUser;
using Kart.Identity.Application.Features.UnlockUser;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>
/// api-contract.yaml `/v1/internal/*` paths — service-to-service only, never
/// exposed through public Gateway routes. Callable only by Admin Service's own
/// Admin-scoped client-credentials service principal (ADR-0010), enforced here by
/// requiring the "admin" OAuth2 scope from `components.securitySchemes.clientCredentials`
/// — a role check would not distinguish this from an interactive Admin user's own
/// bearer token, since both carry a `roles: admin` claim; only the client-credentials
/// grant path ever populates `scopes`.
/// </summary>
public static class InternalUserEndpoints
{
    public static IEndpointRouteBuilder MapInternalUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/internal/users/{userId}/lock", async (string userId, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var lockedBy = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
            await sender.Send(new LockUserCommand(userId, lockedBy), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("scopes", "admin"))
        .WithName("LockUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/v1/internal/users/{userId}/unlock", async (string userId, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var unlockedBy = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
            await sender.Send(new UnlockUserCommand(userId, unlockedBy), cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("scopes", "admin"))
        .WithName("UnlockUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
