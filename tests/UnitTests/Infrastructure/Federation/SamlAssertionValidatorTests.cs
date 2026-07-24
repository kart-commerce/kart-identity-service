using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Infrastructure.Federation;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

public class SamlAssertionValidatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string SpEntityId = "kart-identity-service";
    private readonly SamlAssertionValidator _validator = new();

    [Fact]
    public void ValidateAndExtract_ValidSignedAssertion_ReturnsExtractedClaims()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            SpEntityId, "alice@example.com", ["Engineering", "Ops"], FixedNow);
        var idp = CreateIdp(certificate);

        var result = _validator.ValidateAndExtract(samlResponse, idp, FixedNow);

        Assert.Equal("alice@example.com", result.NameId);
        Assert.Equal(["Engineering", "Ops"], result.GroupClaims);
        Assert.False(string.IsNullOrEmpty(result.AssertionId));
    }

    [Fact]
    public void ValidateAndExtract_TamperedSignature_Throws()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            SpEntityId, "alice@example.com", [], FixedNow);
        var idp = CreateIdp(certificate);

        var tamperedXml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse))
            .Replace("alice@example.com", "mallory@example.com");
        var tamperedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tamperedXml));

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract(tamperedResponse, idp, FixedNow));
    }

    [Fact]
    public void ValidateAndExtract_WrongCertificate_Throws()
    {
        var (samlResponse, _) = TestSamlResponseBuilder.BuildSignedResponse(SpEntityId, "alice@example.com", [], FixedNow);
        var (_, otherCertificate) = TestSamlResponseBuilder.BuildSignedResponse(SpEntityId, "someone-else@example.com", [], FixedNow);
        var idp = CreateIdp(otherCertificate);

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract(samlResponse, idp, FixedNow));
    }

    [Fact]
    public void ValidateAndExtract_ExpiredAssertion_Throws()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            SpEntityId, "alice@example.com", [], FixedNow, validity: TimeSpan.FromMinutes(5));
        var idp = CreateIdp(certificate);

        Assert.Throws<InvalidSamlAssertionException>(
            () => _validator.ValidateAndExtract(samlResponse, idp, FixedNow.AddMinutes(10)));
    }

    [Fact]
    public void ValidateAndExtract_WrongAudience_Throws()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            "some-other-service", "alice@example.com", [], FixedNow);
        var idp = CreateIdp(certificate);

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract(samlResponse, idp, FixedNow));
    }

    [Fact]
    public void ValidateAndExtract_NonSuccessStatus_Throws()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            SpEntityId, "alice@example.com", [], FixedNow, statusCode: "urn:oasis:names:tc:SAML:2.0:status:Requester");
        var idp = CreateIdp(certificate);

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract(samlResponse, idp, FixedNow));
    }

    [Fact]
    public void ValidateAndExtract_NoSignature_Throws()
    {
        var (samlResponse, certificate) = TestSamlResponseBuilder.BuildSignedResponse(
            SpEntityId, "alice@example.com", [], FixedNow, includeSignature: false);
        var idp = CreateIdp(certificate);

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract(samlResponse, idp, FixedNow));
    }

    [Fact]
    public void ValidateAndExtract_MalformedBase64_Throws()
    {
        var idp = CreateIdp(TestSamlResponseBuilder.BuildSignedResponse(SpEntityId, "alice@example.com", [], FixedNow).Certificate);

        Assert.Throws<InvalidSamlAssertionException>(() => _validator.ValidateAndExtract("not-valid-base64!!!", idp, FixedNow));
    }

    private static EnterpriseIdpDescriptor CreateIdp(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate) =>
        new("test-idp", "https://idp.example.com/sso", SpEntityId, "https://identity.example.com/acs", certificate.ExportCertificatePem());
}
