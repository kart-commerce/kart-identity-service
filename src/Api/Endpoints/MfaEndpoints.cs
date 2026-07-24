using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kart.Identity.Application.Features.ConfirmMfaEnrollment;
using Kart.Identity.Application.Features.EnrollMfa;
using Kart.Identity.Application.Features.VerifyMfa;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>
/// api-contract.yaml `/v1/auth/mfa/*` paths. Enroll/enroll-confirm are
/// bearer-authenticated (a logged-in user opting into MFA); verify is
/// deliberately not — it completes the pre-auth challenge /auth/login issued
/// before any token existed (edge-cases.md, "Partial-Auth Window During MFA").
/// </summary>
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

        app.MapPost("/v1/auth/mfa/enroll/confirm", async (ConfirmMfaEnrollmentRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await sender.Send(new ConfirmMfaEnrollmentCommand(userId, request.TotpCode), cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization()
        .WithName("ConfirmMfaEnrollment")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/auth/mfa/verify", async (VerifyMfaRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new VerifyMfaCommand(request.ChallengeId, request.TotpCode), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("VerifyMfa")
        .Produces<VerifyMfaResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private sealed record ConfirmMfaEnrollmentRequest(string TotpCode);

    private sealed record VerifyMfaRequest(string ChallengeId, string TotpCode);
}
