namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// A freshly generated TOTP (RFC 6238) secret plus its otpauth:// provisioning URI
/// (for an authenticator app / QR code) — <see cref="Secret"/> is the plaintext
/// base32 secret; callers must encrypt it (<see cref="IMfaSecretCipher"/>) before
/// persisting, never store it as-is.
/// </summary>
public sealed record TotpEnrollment(string Secret, string ProvisioningUri);
