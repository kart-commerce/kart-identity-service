using Kart.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class AesMfaSecretCipherTests
{
    private readonly AesMfaSecretCipher _cipher = new(Options.Create(new MfaEncryptionOptions
    {
        KeyBase64 = Convert.ToBase64String(new byte[32]) // deterministic all-zero test key
    }));

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsToOriginalSecret()
    {
        var ciphertext = _cipher.Encrypt("JBSWY3DPEHPK3PXP");

        var decrypted = _cipher.Decrypt(ciphertext);

        Assert.Equal("JBSWY3DPEHPK3PXP", decrypted);
    }

    [Fact]
    public void Encrypt_DoesNotStoreThePlaintextBytesVerbatim()
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes("JBSWY3DPEHPK3PXP");

        var ciphertext = _cipher.Encrypt("JBSWY3DPEHPK3PXP");

        // nonce (12) + ciphertext + tag (16) is always longer than the plaintext
        // alone, and is never byte-for-byte equal to it.
        Assert.True(ciphertext.Length > plaintextBytes.Length);
        Assert.NotEqual(plaintextBytes, ciphertext);
    }

    [Fact]
    public void Encrypt_SameSecretTwice_ProducesDifferentCiphertext()
    {
        var first = _cipher.Encrypt("JBSWY3DPEHPK3PXP");
        var second = _cipher.Encrypt("JBSWY3DPEHPK3PXP");

        Assert.NotEqual(first, second); // random nonce per call
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsRatherThanReturningWrongPlaintext()
    {
        var ciphertext = _cipher.Encrypt("JBSWY3DPEHPK3PXP");
        ciphertext[^1] ^= 0xFF; // flip a bit in the auth tag

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => _cipher.Decrypt(ciphertext));
    }
}
