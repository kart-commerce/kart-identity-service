using Kart.Identity.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>kart-web's own routes (account.routes.ts): `/account/password-reset/confirm?token=`.</summary>
public sealed class PublicWebLinkBuilder(IConfiguration configuration) : IPublicWebLinkBuilder
{
    public string PasswordResetConfirmLink(string rawResetToken)
    {
        var baseUrl = configuration["PublicWebBaseUrl"]!.TrimEnd('/');
        return $"{baseUrl}/account/password-reset/confirm?token={Uri.EscapeDataString(rawResetToken)}";
    }
}
