namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/register 409 — "Email already registered."</summary>
public sealed class EmailAlreadyRegisteredException(string email)
    : Exception($"An account with email '{email}' already exists.");
