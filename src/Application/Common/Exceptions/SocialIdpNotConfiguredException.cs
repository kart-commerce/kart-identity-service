namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml GET /auth/sso/social/{provider}/login 404 — "provider not configured."</summary>
public sealed class SocialIdpNotConfiguredException(string provider) : Exception($"Social IdP '{provider}' is not configured.");
