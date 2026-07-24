using System.Security.Cryptography;
using System.Text;
using Kart.Identity.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// AES-256-GCM (authenticated encryption) for the TOTP secret at rest
/// (requirement-spec.md §4's PII invariant). Stored layout is one column
/// (database-design.md `mfa_credentials.encrypted_secret`): nonce || ciphertext || tag.
/// </summary>
public sealed class AesMfaSecretCipher : IMfaSecretCipher
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesMfaSecretCipher(IOptions<MfaEncryptionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.KeyBase64);
    }

    public byte[] Encrypt(string plaintextSecret)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextSecret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var result = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes + ciphertext.Length, TagSizeBytes);
        return result;
    }

    public string Decrypt(byte[] ciphertext)
    {
        var nonce = ciphertext[..NonceSizeBytes];
        var tag = ciphertext[^TagSizeBytes..];
        var actualCiphertext = ciphertext[NonceSizeBytes..^TagSizeBytes];
        var plaintextBytes = new byte[actualCiphertext.Length];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, actualCiphertext, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
