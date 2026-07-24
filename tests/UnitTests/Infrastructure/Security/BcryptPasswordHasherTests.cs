using Kart.Identity.Infrastructure.Security;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_NeverReturnsThePlaintext()
    {
        var hash = _hasher.Hash("SuperSecret1");

        Assert.NotEqual("SuperSecret1", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var first = _hasher.Hash("SuperSecret1");
        var second = _hasher.Hash("SuperSecret1");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_ProducesAVerifiableBcryptHash()
    {
        var hash = _hasher.Hash("SuperSecret1");

        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify("SuperSecret1", hash));
        Assert.False(BCrypt.Net.BCrypt.EnhancedVerify("WrongPassword", hash));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("SuperSecret1");

        Assert.True(_hasher.Verify("SuperSecret1", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("SuperSecret1");

        Assert.False(_hasher.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Verify_NullHash_ReturnsFalse()
    {
        // /auth/login's timing-attack mitigation path (unknown account or a
        // federated account with no native password) — must not throw.
        Assert.False(_hasher.Verify("AnyPassword1", null));
    }
}
