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
}
