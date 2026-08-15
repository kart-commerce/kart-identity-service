using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kart.Identity.Application.Common;
using Kart.Identity.Application.Features.ConfirmMfaEnrollment;
using Kart.Identity.Application.Features.EnrollMfa;
using Kart.Identity.Application.Features.VerifyMfa;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

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
        app.MapPost("/v1/auth/mfa/enroll", async (HttpContext httpContext, ISender sender, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            logger.LogInformation("Stage {Stage}: MFA enroll request received for user {UserId}", "EnrollMfaRequestReceived", userId);
            logger.LogInformation("Stage {Stage}: dispatching EnrollMfaCommand for user {UserId}", "EnrollMfaCommandDispatched", userId);
            var response = await sender.Send(new EnrollMfaCommand(userId), cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("EnrollMfa")
        .Produces<EnrollMfaResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/auth/mfa/enroll/confirm", async (ConfirmMfaEnrollmentRequest request, HttpContext httpContext, ISender sender, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            logger.LogInformation("Stage {Stage}: MFA enroll confirm request received for user {UserId}", "ConfirmMfaEnrollmentRequestReceived", userId);
            logger.LogInformation("Stage {Stage}: dispatching ConfirmMfaEnrollmentCommand for user {UserId}", "ConfirmMfaEnrollmentCommandDispatched", userId);
            await sender.Send(new ConfirmMfaEnrollmentCommand(userId, request.TotpCode), cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization()
        .WithName("ConfirmMfaEnrollment")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/auth/mfa/verify", async (VerifyMfaRequest request, ISender sender, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            using var _ = KartFlowContext.Push(FlowNames.UserRegistrationLoginAuthentication);
            logger.LogInformation("Stage {Stage}: MFA verify request received for challenge {ChallengeId}", "VerifyMfaRequestReceived", request.ChallengeId);
            logger.LogInformation("Stage {Stage}: dispatching VerifyMfaCommand for challenge {ChallengeId}", "VerifyMfaCommandDispatched", request.ChallengeId);
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
