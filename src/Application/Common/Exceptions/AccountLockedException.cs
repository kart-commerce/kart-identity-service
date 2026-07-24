namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/login 423 — admin-triggered or progressive-throttle lockout.</summary>
public sealed class AccountLockedException() : Exception("This account is locked.");
