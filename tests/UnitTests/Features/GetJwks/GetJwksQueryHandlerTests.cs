using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.GetJwks;
using Xunit;

namespace Kart.Identity.UnitTests.Features.GetJwks;

public class GetJwksQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsKeysFromKeyProvider()
    {
        var expectedKeys = new[]
        {
            new JsonWebKey(Kty: "RSA", Use: "sig", Alg: "RS256", Kid: "test-kid", N: "modulus", E: "AQAB")
        };
        var handler = new GetJwksQueryHandler(new FakeJwtKeyProvider(expectedKeys));

        var response = await handler.Handle(new GetJwksQuery(), CancellationToken.None);

        Assert.Same(expectedKeys, response.Keys);
    }

    private sealed class FakeJwtKeyProvider(IReadOnlyList<JsonWebKey> keys) : IJwtKeyProvider
    {
        public IReadOnlyList<JsonWebKey> GetPublicSigningKeys() => keys;
    }
}
