namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/oidc/callback and
/// GET /auth/sso/social/{provider}/callback, both 401 — "invalid code/state, or
/// token exchange with the IdP failed." Mirrors <see cref="InvalidSamlAssertionException"/>'s
/// treatment of an unconfigured idpAlias at the callback endpoint: neither
/// callback names a 404 of its own, so an unconfigured alias/provider is folded
/// into this same case rather than added as a new response code.
/// </summary>
public sealed class InvalidOidcTokenException(string reason) : Exception(reason);
