namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/token 401 — "Invalid client_id/client_secret."</summary>
public sealed class InvalidServicePrincipalCredentialsException() : Exception("Invalid client_id or client_secret.");
