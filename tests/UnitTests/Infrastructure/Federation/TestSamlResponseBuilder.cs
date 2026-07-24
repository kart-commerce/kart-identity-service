using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

/// <summary>
/// Test-only helper that builds a signed SAML 2.0 Response using a fresh
/// self-signed certificate, mirroring the shape <c>SamlAssertionValidator</c>
/// expects — lets tests exercise the real signature-verification/XML-parsing
/// path rather than mocking it away.
/// </summary>
public static class TestSamlResponseBuilder
{
    public static (string SamlResponseBase64, X509Certificate2 Certificate) BuildSignedResponse(
        string audience,
        string nameId,
        IEnumerable<string> groups,
        DateTimeOffset now,
        TimeSpan? validity = null,
        string? assertionId = null,
        string? statusCode = null,
        bool includeSignature = true)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test-idp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeralCert = request.CreateSelfSigned(now.AddDays(-1).UtcDateTime, now.AddDays(1).UtcDateTime);
        var certificate = new X509Certificate2(ephemeralCert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);

        var effectiveValidity = validity ?? TimeSpan.FromMinutes(5);
        var effectiveAssertionId = assertionId ?? $"_{Guid.NewGuid():N}";
        var effectiveStatus = statusCode ?? "urn:oasis:names:tc:SAML:2.0:status:Success";
        var issueInstant = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var notOnOrAfter = (now + effectiveValidity).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var groupValues = string.Concat(groups.Select(g => $"<saml:AttributeValue>{g}</saml:AttributeValue>"));

        var responseXml =
            $"""
             <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_{Guid.NewGuid():N}" Version="2.0" IssueInstant="{issueInstant}">
               <saml:Issuer>test-idp</saml:Issuer>
               <samlp:Status><samlp:StatusCode Value="{effectiveStatus}" /></samlp:Status>
               <saml:Assertion ID="{effectiveAssertionId}" Version="2.0" IssueInstant="{issueInstant}">
                 <saml:Issuer>test-idp</saml:Issuer>
                 <saml:Subject><saml:NameID>{nameId}</saml:NameID></saml:Subject>
                 <saml:Conditions NotBefore="{issueInstant}" NotOnOrAfter="{notOnOrAfter}">
                   <saml:AudienceRestriction><saml:Audience>{audience}</saml:Audience></saml:AudienceRestriction>
                 </saml:Conditions>
                 <saml:AttributeStatement><saml:Attribute Name="Group">{groupValues}</saml:Attribute></saml:AttributeStatement>
               </saml:Assertion>
             </samlp:Response>
             """;

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(responseXml);

        if (includeSignature)
        {
            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            var assertionElement = (XmlElement)doc.SelectSingleNode("//saml:Assertion", nsManager)!;
            SignElement(doc, assertionElement, certificate);
        }

        var finalXml = doc.OuterXml;
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(finalXml));
        return (base64, certificate);
    }

    private static void SignElement(XmlDocument doc, XmlElement elementToSign, X509Certificate2 certificate)
    {
        var elementId = elementToSign.GetAttribute("ID");

        var signedXml = new SignedXml(doc) { SigningKey = certificate.GetRSAPrivateKey() };
        var reference = new Reference("#" + elementId);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);
        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();
        var imported = doc.ImportNode(signatureElement, true);
        var issuerNode = elementToSign.SelectSingleNode("*[local-name()='Issuer']");
        elementToSign.InsertAfter(imported, issuerNode);
    }
}
