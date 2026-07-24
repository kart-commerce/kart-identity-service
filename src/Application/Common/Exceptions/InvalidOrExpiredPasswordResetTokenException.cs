namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/password/reset-confirm 400 — "Reset token
/// invalid, expired, or already used."
/// </summary>
public sealed class InvalidOrExpiredPasswordResetTokenException() : Exception("Reset token invalid, expired, or already used.");
