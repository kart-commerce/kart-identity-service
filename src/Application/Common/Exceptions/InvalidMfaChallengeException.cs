namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/mfa/verify 401 — "Incorrect code, or challengeId
/// unknown/expired." One generic response for both causes, same non-disclosure
/// treatment as InvalidCredentialsException.
/// </summary>
public sealed class InvalidMfaChallengeException() : Exception("Incorrect code, or challenge unknown or expired.");
