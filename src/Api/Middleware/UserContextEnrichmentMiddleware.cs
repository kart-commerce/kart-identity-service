using Serilog.Context;

namespace Kart.Identity.Api.Middleware;

/// <summary>
/// observability-standards.md / requirement-spec.md's Observability NFR row: every
/// structured log carries `userId` alongside the mandatory `traceId`/`spanId`/`service`/
/// `level` fields. Runs after <c>UseAuthentication</c>, so <c>HttpContext.User</c> is
/// already populated for bearer-protected endpoints; a no-op for anonymous requests
/// (login, register, JWKS) since there is no subject yet to attach.
/// </summary>
public sealed class UserContextEnrichmentMiddleware(RequestDelegate next)
{
    // JwtRegisteredClaimNames.Sub ("sub") — MapInboundClaims is disabled
    // (Infrastructure/DependencyInjection.cs), so the claim type is the raw "sub",
    // never remapped to ClaimTypes.NameIdentifier.
    private const string SubjectClaimType = "sub";

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(SubjectClaimType)?.Value
            : null;

        if (userId is null)
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty("userId", userId))
        {
            await next(context);
        }
    }
}
