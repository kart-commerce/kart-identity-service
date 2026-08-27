namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>api-contract.yaml POST /auth/otp/request and /auth/otp/verify 429 — same progressive per-account+per-IP throttle shape as LoginRateLimitExceededException, applied to both requesting and verifying a one-time code.</summary>
public sealed class OtpRateLimitExceededException() : Exception("Too many OTP attempts. Try again later.");
