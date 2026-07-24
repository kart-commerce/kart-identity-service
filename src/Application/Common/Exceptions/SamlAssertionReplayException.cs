namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs 409 — "Assertion ID already consumed (replay)."</summary>
public sealed class SamlAssertionReplayException() : Exception("SAML assertion has already been consumed.");
