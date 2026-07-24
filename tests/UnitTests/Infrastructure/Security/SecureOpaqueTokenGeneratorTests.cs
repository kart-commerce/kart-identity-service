using Kart.Identity.Infrastructure.Security;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class SecureOpaqueTokenGeneratorTests
{
    private readonly SecureOpaqueTokenGenerator _generator = new();

    [Fact]
    public void Generate_ProducesUniqueUrlSafeValues()
    {
        var first = _generator.Generate();
        var second = _generator.Generate();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
