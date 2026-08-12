namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Builds browser-reachable links embedded in outbound notifications (e.g. the password-reset
/// confirm link) — deliberately distinct from any Docker-internal service-to-service base URL,
/// since these links are opened by the end user's own browser.
/// </summary>
public interface IPublicWebLinkBuilder
{
    string PasswordResetConfirmLink(string rawResetToken);
}
