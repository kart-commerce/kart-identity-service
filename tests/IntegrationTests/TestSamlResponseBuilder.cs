using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Test-only helper that builds a signed SAML 2.0 Response, signed with a
/// caller-supplied certificate (the same one the test host's `test-idp` is
/// configured to trust — see <see cref="IdentityApiFactory.TestIdpCertificate"/>)
/// — lets tests exercise the real signature-verification/XML-parsing path over
/// HTTP rather than mocking it away.
/// </summary>
public static class TestSamlResponseBuilder
{
    public static string BuildSignedResponse(
        X509Certificate2 certificate,
        string audience,
        string nameId,
        IEnumerable<string> groups,
        DateTimeOffset now,
        TimeSpan? validity = null,
        string? assertionId = null)
    {
        var effectiveValidity = validity ?? TimeSpan.FromMinutes(5);
        var effectiveAssertionId = assertionId ?? $"_{Guid.NewGuid():N}";
        var issueInstant = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var notOnOrAfter = (now + effectiveValidity).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var groupValues = string.Concat(groups.Select(g => $"<saml:AttributeValue>{g}</saml:AttributeValue>"));

        var responseXml =
            $"""
             <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_{Guid.NewGuid():N}" Version="2.0" IssueInstant="{issueInstant}">
               <saml:Issuer>test-idp</saml:Issuer>
               <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" /></samlp:Status>
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

        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
        var assertionElement = (XmlElement)doc.SelectSingleNode("//saml:Assertion", nsManager)!;
        SignElement(doc, assertionElement, certificate);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(doc.OuterXml));
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
