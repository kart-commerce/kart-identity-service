using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kart.Identity.Application.Features.ConfirmPasswordReset;
using Kart.Identity.Application.Features.InitiatePasswordReset;
using Kart.Identity.Application.Features.IssueServicePrincipalToken;
using Kart.Identity.Application.Features.Login;
using Kart.Identity.Application.Features.Logout;
using Kart.Identity.Application.Features.RotateRefreshToken;
using Kart.Identity.Application.Features.RegisterUser;
using Kart.Identity.Application.Features.UpdateProfile;
using MediatR;

namespace Kart.Identity.Api.Endpoints;

/// <summary>api-contract.yaml `/v1` native AuthN/registration paths.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/register", async (RegisterRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.Password, request.DisplayName);
            var response = await sender.Send(command, cancellationToken);
            return Results.Created((string?)null, response);
        })
        .WithName("RegisterUser")
        .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/v1/auth/login", async (LoginRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new LoginCommand(request.Email, request.Password, ipAddress);
            var result = await sender.Send(command, cancellationToken);

            return result switch
            {
                AuthenticatedLoginResult authenticated => Results.Ok(authenticated),
                MfaChallengeLoginResult challenge => Results.Json(challenge, statusCode: StatusCodes.Status202Accepted),
                _ => throw new InvalidOperationException($"Unhandled {nameof(LoginResult)} subtype: {result.GetType()}")
            };
        })
        .WithName("Login")
        .Produces<AuthenticatedLoginResult>(StatusCodes.Status200OK)
        .Produces<MfaChallengeLoginResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status423Locked)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        app.MapPost("/v1/auth/token", async (HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var command = new IssueServicePrincipalTokenCommand(
                form["grant_type"].ToString(),
                form["client_id"].ToString(),
                form["client_secret"].ToString(),
                form["scope"].ToString() is { Length: > 0 } scope ? scope : null);
            var response = await sender.Send(command, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("IssueServicePrincipalToken")
        .Accepts<IFormCollection>("application/x-www-form-urlencoded")
        .Produces<IssueServicePrincipalTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/auth/refresh", async (RefreshRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new RotateRefreshTokenCommand(request.RefreshToken), cancellationToken);
            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .Produces<RotateRefreshTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/v1/auth/logout", async (LogoutRequest? request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var jti = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Jti)!;
            var expSeconds = long.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Exp)!);
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);

            var command = new LogoutCommand(userId, jti, expiresAt, request?.RefreshToken);
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPatch("/v1/auth/profile", async (UpdateProfileRequest request, HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
        {
            var userId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var response = await sender.Send(new UpdateProfileCommand(userId, request.Email, request.DisplayName), cancellationToken);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("UpdateProfile")
        .Produces<UpdateProfileResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/v1/auth/password/reset-initiate", async (InitiatePasswordResetRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new InitiatePasswordResetCommand(request.Email), cancellationToken);
            return Results.Accepted();
        })
        .WithName("InitiatePasswordReset")
        .Produces(StatusCodes.Status202Accepted);

        app.MapPost("/v1/auth/password/reset-confirm", async (ConfirmPasswordResetRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new ConfirmPasswordResetCommand(request.ResetToken, request.NewPassword), cancellationToken);
            return Results.Ok();
        })
        .WithName("ConfirmPasswordReset")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private sealed record RegisterRequest(string Email, string Password, string? DisplayName);

    private sealed record LoginRequest(string Email, string Password);

    private sealed record RefreshRequest(string RefreshToken);

    private sealed record LogoutRequest(string? RefreshToken);

    private sealed record UpdateProfileRequest(string? Email, string? DisplayName);

    private sealed record InitiatePasswordResetRequest(string Email);

    private sealed record ConfirmPasswordResetRequest(string ResetToken, string NewPassword);
}
