using Kart.Identity.Infrastructure.Security;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class Sha256TokenHasherTests
{
    private readonly Sha256TokenHasher _hasher = new();

    [Fact]
    public void Hash_IsDeterministic()
    {
        Assert.Equal(_hasher.Hash("same-token"), _hasher.Hash("same-token"));
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHashes()
    {
        Assert.NotEqual(_hasher.Hash("token-a"), _hasher.Hash("token-b"));
    }

    [Fact]
    public void Hash_NeverReturnsTheRawToken()
    {
        Assert.NotEqual("raw-token", _hasher.Hash("raw-token"));
    }
}
