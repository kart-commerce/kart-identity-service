namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/mfa/enroll/confirm 400 — covers no pending
/// enrollment, an expired one, and a wrong code alike (a single generic
/// response, matching this codebase's non-disclosure treatment elsewhere,
/// e.g. InvalidCredentialsException not distinguishing "no such account"
/// from "wrong password").
/// </summary>
public sealed class InvalidOrExpiredMfaCodeException() : Exception("Invalid or expired MFA code.");
