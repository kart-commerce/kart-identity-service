using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// Parses and validates a SAML 2.0 Response using .NET's built-in XML-DSig
/// (<see cref="SignedXml"/>) — a signed assertion needs no back-channel call to
/// the IdP to verify, unlike OIDC's authorization-code exchange.
/// </summary>
public sealed class SamlAssertionValidator : ISamlAssertionValidator
{
    private const string SamlAssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string SamlProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string DsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const string SuccessStatus = "urn:oasis:names:tc:SAML:2.0:status:Success";

    /// <summary>
    /// Engineering default: no design doc names the exact IdP attribute Identity
    /// reads for role-mapping group claims.
    /// </summary>
    private const string GroupAttributeName = "Group";

    public SamlAssertionResult ValidateAndExtract(string samlResponseBase64, EnterpriseIdpDescriptor idp, DateTimeOffset now)
    {
        var doc = LoadXml(samlResponseBase64);

        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("samlp", SamlProtocolNamespace);
        nsManager.AddNamespace("saml", SamlAssertionNamespace);
        nsManager.AddNamespace("ds", DsigNamespace);

        var statusValue = doc.SelectSingleNode("//samlp:Status/samlp:StatusCode", nsManager)?.Attributes?["Value"]?.Value;
        if (statusValue != SuccessStatus)
        {
            throw new InvalidSamlAssertionException($"status was not Success ({statusValue ?? "missing"})");
        }

        VerifySignature(doc, nsManager, idp.SigningCertificatePem);

        var assertionNode = doc.SelectSingleNode("//saml:Assertion", nsManager)
            ?? throw new InvalidSamlAssertionException("no assertion present");

        var assertionId = assertionNode.Attributes?["ID"]?.Value
            ?? throw new InvalidSamlAssertionException("assertion has no ID");

        ValidateConditions(assertionNode, nsManager, idp.SpEntityId, now, out var notOnOrAfter);

        var nameId = assertionNode.SelectSingleNode("saml:Subject/saml:NameID", nsManager)?.InnerText
            ?? throw new InvalidSamlAssertionException("assertion has no Subject NameID");

        var groupClaims = ExtractGroupClaims(assertionNode, nsManager);

        return new SamlAssertionResult(assertionId, nameId, groupClaims, notOnOrAfter);
    }

    private static XmlDocument LoadXml(string samlResponseBase64)
    {
        try
        {
            var xmlBytes = Convert.FromBase64String(samlResponseBase64);
            var doc = new XmlDocument { PreserveWhitespace = true };
            using var stream = new MemoryStream(xmlBytes);
            doc.Load(stream);
            return doc;
        }
        catch (Exception ex) when (ex is FormatException or XmlException)
        {
            throw new InvalidSamlAssertionException("malformed SAMLResponse");
        }
    }

    private static void VerifySignature(XmlDocument doc, XmlNamespaceManager nsManager, string signingCertificatePem)
    {
        var signatureNode = doc.SelectSingleNode("//ds:Signature", nsManager) as XmlElement
            ?? throw new InvalidSamlAssertionException("no signature present");

        X509Certificate2 certificate;
        try
        {
            certificate = X509Certificate2.CreateFromPem(signingCertificatePem);
        }
        catch (CryptographicUnexpectedOperationException)
        {
            throw new InvalidSamlAssertionException("IdP signing certificate is misconfigured");
        }

        var signedXml = new SignedXml(doc);
        signedXml.LoadXml(signatureNode);
        if (!signedXml.CheckSignature(certificate, true))
        {
            throw new InvalidSamlAssertionException("signature verification failed");
        }
    }

    private static void ValidateConditions(
        XmlNode assertionNode, XmlNamespaceManager nsManager, string spEntityId, DateTimeOffset now, out DateTimeOffset notOnOrAfter)
    {
        var conditionsNode = assertionNode.SelectSingleNode("saml:Conditions", nsManager);
        var notBefore = ParseInstant(conditionsNode?.Attributes?["NotBefore"]?.Value);
        notOnOrAfter = ParseInstant(conditionsNode?.Attributes?["NotOnOrAfter"]?.Value)
            ?? throw new InvalidSamlAssertionException("assertion has no NotOnOrAfter");

        if (notBefore is not null && now < notBefore)
        {
            throw new InvalidSamlAssertionException("assertion not yet valid");
        }

        if (now >= notOnOrAfter)
        {
            throw new InvalidSamlAssertionException("assertion expired");
        }

        var audience = assertionNode.SelectSingleNode("saml:Conditions/saml:AudienceRestriction/saml:Audience", nsManager)?.InnerText;
        if (audience != spEntityId)
        {
            throw new InvalidSamlAssertionException("audience does not match this service");
        }
    }

    private static List<string> ExtractGroupClaims(XmlNode assertionNode, XmlNamespaceManager nsManager)
    {
        var groupClaims = new List<string>();
        var groupValueNodes = assertionNode.SelectNodes(
            $"saml:AttributeStatement/saml:Attribute[@Name='{GroupAttributeName}']/saml:AttributeValue", nsManager);
        if (groupValueNodes is null)
        {
            return groupClaims;
        }

        foreach (XmlNode valueNode in groupValueNodes)
        {
            groupClaims.Add(valueNode.InnerText);
        }

        return groupClaims;
    }

    private static DateTimeOffset? ParseInstant(string? value) =>
        value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
