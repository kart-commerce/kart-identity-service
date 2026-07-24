namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Reversible AES-256 encryption for the TOTP secret at rest
/// (requirement-spec.md §4's PII invariant; database-design.md
/// `mfa_credentials.encrypted_secret`) — unlike <see cref="IPasswordHasher"/>,
/// validating a submitted TOTP code requires recovering the original secret, so
/// this must be reversible rather than a one-way hash.
/// </summary>
public interface IMfaSecretCipher
{
    byte[] Encrypt(string plaintextSecret);

    string Decrypt(byte[] ciphertext);
}
