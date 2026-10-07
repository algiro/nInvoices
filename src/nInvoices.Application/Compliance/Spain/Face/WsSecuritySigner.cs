using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Compliance.Spain.Face;

/// <summary>
/// Signs a SOAP message as FACe requires (OASIS WS-Security 1.0, X.509 token profile): the certificate travels
/// as a binary security token in the header, with a signature of the body (exclusive canonicalization) that
/// points at it, and a timestamp valid for five minutes. The layout is the one in FACe's documentation
/// ("Servicios para sistemas automatizados de proveedores", appendix A).
/// </summary>
public static class WsSecuritySigner
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string WsuNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string DsNs = "http://www.w3.org/2000/09/xmldsig#";

    private const string ExcC14n = "http://www.w3.org/2001/10/xml-exc-c14n#";
    private const string TokenEncoding = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";
    private const string TokenValueType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";

    private const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string RsaSha1 = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
    private const string Sha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string Sha1 = "http://www.w3.org/2000/09/xmldsig#sha1";

    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(5);

    /// <summary>Wraps the body in a SOAP envelope and signs it.</summary>
    /// <param name="sha1">Sign with SHA-1 (what FACe documentation shows) instead of SHA-256.</param>
    /// <returns>The signed envelope, ready to send.</returns>
    /// <exception cref="InvalidOperationException">The certificate has no RSA private key.</exception>
    public static string SignedEnvelope(XElement body, X509Certificate2 certificate, bool sha1, DateTime nowUtc)
    {
        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new DomainException("The signing certificate has no RSA private key");

        var id = Guid.NewGuid().ToString("N")[..16];
        var bodyId = $"id-{id}";
        var tokenId = $"CertId-{id}";

        var document = new XmlDocument { PreserveWhitespace = true };
        var envelope = document.CreateElement("soapenv", "Envelope", SoapNs);
        envelope.SetAttribute("xmlns:wsu", WsuNs);
        envelope.SetAttribute("xmlns:wsse", WsseNs);
        document.AppendChild(envelope);

        var header = document.CreateElement("soapenv", "Header", SoapNs);
        envelope.AppendChild(header);

        var bodyElement = document.CreateElement("soapenv", "Body", SoapNs);
        WsuId(document, bodyElement, bodyId);
        bodyElement.SetAttribute("xmlns:wsu", WsuNs); // as the example shows it, on the body itself
        bodyElement.InnerXml = body.ToString(SaveOptions.DisableFormatting);
        envelope.AppendChild(bodyElement);

        var security = document.CreateElement("wsse", "Security", WsseNs);
        var mustUnderstand = document.CreateAttribute("soapenv", "mustUnderstand", SoapNs);
        mustUnderstand.Value = "1";
        security.Attributes.Append(mustUnderstand);
        header.AppendChild(security);

        // The token: the certificate itself
        var token = document.CreateElement("wsse", "BinarySecurityToken", WsseNs);
        token.SetAttribute("EncodingType", TokenEncoding);
        token.SetAttribute("ValueType", TokenValueType);
        WsuId(document, token, tokenId);
        token.InnerText = Convert.ToBase64String(certificate.RawData);
        security.AppendChild(token);

        // The signature: of the body, pointing at the token
        var signature = document.CreateElement("ds", "Signature", DsNs);
        signature.SetAttribute("xmlns:ds", DsNs);
        var signedInfo = document.CreateElement("ds", "SignedInfo", DsNs);
        signedInfo.AppendChild(Ds(document, "CanonicalizationMethod", ("Algorithm", ExcC14n)));
        signedInfo.AppendChild(Ds(document, "SignatureMethod", ("Algorithm", sha1 ? RsaSha1 : RsaSha256)));

        var reference = Ds(document, "Reference", ("URI", $"#{bodyId}"));
        var transforms = Ds(document, "Transforms");
        transforms.AppendChild(Ds(document, "Transform", ("Algorithm", ExcC14n)));
        reference.AppendChild(transforms);
        reference.AppendChild(Ds(document, "DigestMethod", ("Algorithm", sha1 ? Sha1 : Sha256)));
        var digest = Ds(document, "DigestValue");
        reference.AppendChild(digest);
        signedInfo.AppendChild(reference);
        signature.AppendChild(signedInfo);

        var signatureValue = Ds(document, "SignatureValue");
        signature.AppendChild(signatureValue);

        var keyInfo = Ds(document, "KeyInfo");
        var tokenReference = document.CreateElement("wsse", "SecurityTokenReference", WsseNs);
        var pointer = document.CreateElement("wsse", "Reference", WsseNs);
        pointer.SetAttribute("URI", $"#{tokenId}");
        pointer.SetAttribute("ValueType", TokenValueType);
        tokenReference.AppendChild(pointer);
        keyInfo.AppendChild(tokenReference);
        signature.AppendChild(keyInfo);
        security.AppendChild(signature);

        var timestamp = document.CreateElement("wsu", "Timestamp", WsuNs);
        WsuId(document, timestamp, $"Timestamp-{id}");
        timestamp.AppendChild(Wsu(document, "Created", Format(nowUtc)));
        timestamp.AppendChild(Wsu(document, "Expires", Format(nowUtc + Validity)));
        security.AppendChild(timestamp);

        // The digest of the body, then the signature of the signed info that holds it
        digest.InnerText = Convert.ToBase64String(Hash(sha1, Canonicalize(bodyElement)));
        signatureValue.InnerText = Convert.ToBase64String(rsa.SignData(
            Canonicalize(signedInfo), sha1 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return document.OuterXml;
    }

    /// <summary>The wsu:Id attribute, with its prefix set (canonicalization writes the prefix the node carries).</summary>
    private static void WsuId(XmlDocument document, XmlElement element, string value)
    {
        var attribute = document.CreateAttribute("wsu", "Id", WsuNs);
        attribute.Value = value;
        element.Attributes.Append(attribute);
    }

    private static string Format(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

#pragma warning disable CA5350 // FACe signs with rsa-sha1 when asked to: the protocol, not a choice
    private static byte[] Hash(bool sha1, byte[] data) => sha1 ? SHA1.HashData(data) : SHA256.HashData(data);
#pragma warning restore CA5350

    /// <summary>
    /// Exclusive canonical form of an element as it sits in its document: on its own, with the namespace
    /// declarations in scope (exclusive canonicalization keeps those the element visibly uses).
    /// </summary>
    private static byte[] Canonicalize(XmlElement element)
    {
        var standalone = new XmlDocument { PreserveWhitespace = true };
        var copy = (XmlElement)standalone.ImportNode(element, deep: true);
        standalone.AppendChild(copy);

        var inherited = new Dictionary<string, string>();
        for (XmlNode? node = element; node is XmlElement current; node = node.ParentNode)
        {
            foreach (XmlAttribute attribute in current.Attributes)
            {
                if (attribute.Prefix == "xmlns")
                    inherited.TryAdd(attribute.LocalName, attribute.Value);
                else if (attribute is { Prefix: "", LocalName: "xmlns" })
                    inherited.TryAdd("", attribute.Value);
            }

            // A prefix used by the element but declared by the library only implicitly
            if (current.Prefix.Length > 0)
                inherited.TryAdd(current.Prefix, current.NamespaceURI);
        }

        foreach (var (prefix, uri) in inherited)
        {
            var name = prefix.Length == 0 ? "xmlns" : $"xmlns:{prefix}";
            if (!copy.HasAttribute(name))
                copy.SetAttribute(name, uri);
        }

        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(standalone);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var memory = new MemoryStream();
        output.CopyTo(memory);
        return memory.ToArray();
    }

    private static XmlElement Ds(XmlDocument document, string name, params (string Name, string Value)[] attributes)
    {
        var element = document.CreateElement("ds", name, DsNs);
        foreach (var (attributeName, value) in attributes)
            element.SetAttribute(attributeName, value);
        return element;
    }

    private static XmlElement Wsu(XmlDocument document, string name, string text)
    {
        var element = document.CreateElement("wsu", name, WsuNs);
        element.InnerText = text;
        return element;
    }
}
