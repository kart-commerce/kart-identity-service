namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login 404 — "idpAlias not configured."</summary>
public sealed class EnterpriseIdpNotConfiguredException(string idpAlias) : Exception($"Enterprise IdP '{idpAlias}' is not configured.");
