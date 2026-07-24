namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/login 429 — edge-cases.md "Credential Stuffing / Brute-Force" progressive throttle tripped.</summary>
public sealed class LoginRateLimitExceededException() : Exception("Too many login attempts. Try again later.");
