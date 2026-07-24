namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs 401 —
/// "Assertion invalid, expired, or signature verification failed." Also covers
/// an unconfigured idpAlias at this endpoint, since api-contract.yaml names no
/// 404 for the ACS path specifically (only the login-redirect endpoint does).
/// </summary>
public sealed class InvalidSamlAssertionException(string reason) : Exception($"SAML assertion invalid: {reason}");
