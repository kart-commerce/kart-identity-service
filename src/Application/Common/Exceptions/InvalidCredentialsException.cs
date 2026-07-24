namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/login 401 — "Invalid credentials."</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");
