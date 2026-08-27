namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/otp/verify 400 — covers unknown/expired/already-consumed/wrong-digit code alike, a single generic response, matching InvalidOrExpiredMfaCodeException's non-disclosure treatment.</summary>
public sealed class InvalidOrExpiredOtpCodeException() : Exception("Invalid or expired one-time code.");
