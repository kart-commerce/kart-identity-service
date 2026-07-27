using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.IssueServicePrincipalToken;

/// <summary>
/// api-contract.yaml POST /auth/token — OAuth2 Client Credentials grant
/// (requirement-spec.md §2). Only ever reads `service_principals`; no ticket
/// provisions rows into it (tickets.md's flagged out-of-band-provisioning gap,
/// same shape as native role elevation).
/// </summary>
public sealed class IssueServicePrincipalTokenCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    ILogger<IssueServicePrincipalTokenCommandHandler> logger)
    : IRequestHandler<IssueServicePrincipalTokenCommand, IssueServicePrincipalTokenResponse>
{
    public async Task<IssueServicePrincipalTokenResponse> Handle(IssueServicePrincipalTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = await dbContext.ServicePrincipals
            .SingleOrDefaultAsync(sp => sp.ClientId == request.ClientId, cancellationToken);

        // IPasswordHasher.Verify pays an equivalent-cost dummy check for an
        // unknown client_id, same account-existence timing protection as
        // /auth/login's password check.
        var secretValid = passwordHasher.Verify(request.ClientSecret, principal?.ClientSecretHash);
        if (!secretValid || principal is null || principal.Status != ServicePrincipalStatus.Active)
        {
            throw new InvalidServicePrincipalCredentialsException();
        }

        // database-design.md's `service_principals` has no per-client scope
        // allowlist column to narrow the requested scope against — api-contract.yaml's
        // "narrowed to what the client_id is pre-provisioned for" note is a stated
        // intent this schema doesn't yet back with data; flagged rather than
        // invented, so the requested scope is passed through unmodified for now.
        var scopes = string.IsNullOrWhiteSpace(request.Scope)
            ? []
            : request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var roleClaim = PlatformRoleClaims.ToClaimValue(principal.Role);
        var accessToken = accessTokenGenerator.Generate(principal.ClientId, [roleClaim], scopes);

        logger.LogInformation(
            "Service principal token issued for client {ClientId} with role {Role}",
            principal.ClientId,
            principal.Role);

        return new IssueServicePrincipalTokenResponse(accessToken.Token, "Bearer", accessToken.ExpiresInSeconds, scopes);
    }
}
