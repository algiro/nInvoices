using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Compliance.Spain.Facturae;

/// <summary>
/// Signs a Facturae document as an enveloped XAdES-EPES signature under the Facturae 3.1 signature
/// policy, which Facturae 3.2.x invoices must carry to be accepted by FACe. The signature covers the
/// whole document, the signed XAdES properties (signing time, certificate, policy) and the key info.
/// </summary>
/// <remarks>
/// Every digest is taken over the canonical form (inclusive C14N 1.0) of the node as it sits in the
/// finished document, so a verifier that canonicalizes the node in place gets the same bytes.
/// </remarks>
internal static class XadesEpesSigner
{
    private const string DsNs = "http://www.w3.org/2000/09/xmldsig#";
    private const string XadesNs = "http://uri.etsi.org/01903/v1.3.2#";

    private const string C14nAlgorithm = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
    private const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string Sha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string EnvelopedTransform = "http://www.w3.org/2000/09/xmldsig#enveloped-signature";
    private const string SignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";

    // The published Facturae 3.1 signature policy (valid for every 3.2.x) and the SHA-1 digest it is identified by
    private const string PolicyUrl = "http://www.facturae.es/politica_de_firma_formato_facturae/politica_de_firma_formato_facturae_v3_1.pdf";
    private const string PolicyDescription = "Política de Firma FacturaE v3.1";
    private const string PolicyDigestSha1 = "Ohixl6upD6av8N7pEvDABhEL6hM=";
    private const string Sha1 = "http://www.w3.org/2000/09/xmldsig#sha1";

    /// <summary>Appends the signature to the root element of <paramref name="document"/>.</summary>
    /// <exception cref="InvalidOperationException">The certificate has no RSA private key.</exception>
    public static void Sign(XmlDocument document, X509Certificate2 certificate, DateTime signingTimeUtc)
    {
        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new DomainException("The signing certificate has no RSA private key");

        var id = Guid.NewGuid().ToString("N")[..16];
        var signatureId = $"Signature-{id}";
        var signedPropertiesId = $"SignedProperties-{id}";
        var keyInfoId = $"KeyInfo-{id}";
        var documentReferenceId = $"Reference-{id}";

        var root = document.DocumentElement!;
        var signature = Element(document, "ds", "Signature", DsNs);
        signature.SetAttribute("Id", signatureId);
        root.AppendChild(signature);

        var signedInfo = Element(document, "ds", "SignedInfo", DsNs);
        var signatureValue = Element(document, "ds", "SignatureValue", DsNs);
        signatureValue.SetAttribute("Id", $"SignatureValue-{id}");
        var keyInfo = KeyInfo(document, certificate, rsa, keyInfoId);
        var signedProperties = SignedProperties(document, certificate, signedPropertiesId, documentReferenceId, signingTimeUtc);

        var qualifying = Element(document, "xades", "QualifyingProperties", XadesNs);
        qualifying.SetAttribute("Target", $"#{signatureId}");
        // Declared as an attribute (not just implied by the prefix) so canonicalization sees it; the ds
        // prefix is declared on the Facturae root
        qualifying.SetAttribute("xmlns:xades", XadesNs);
        qualifying.AppendChild(signedProperties);
        var dataObject = Element(document, "ds", "Object", DsNs);
        dataObject.AppendChild(qualifying);

        signature.AppendChild(signedInfo);
        signature.AppendChild(signatureValue);
        signature.AppendChild(keyInfo);
        signature.AppendChild(dataObject);

        // Reference 1: the document without the signature (the enveloped-signature transform)
        var withoutSignature = (XmlDocument)document.CloneNode(true);
        withoutSignature.DocumentElement!.RemoveChild(
            withoutSignature.DocumentElement.GetElementsByTagName("Signature", DsNs)[0]!);

        signedInfo.AppendChild(Element(document, "ds", "CanonicalizationMethod", DsNs, ("Algorithm", C14nAlgorithm)));
        signedInfo.AppendChild(Element(document, "ds", "SignatureMethod", DsNs, ("Algorithm", RsaSha256)));
        signedInfo.AppendChild(Reference(document, documentReferenceId, "", null, Digest(Canonicalize(withoutSignature)), enveloped: true));
        signedInfo.AppendChild(Reference(document, null, $"#{signedPropertiesId}", SignedPropertiesType, Digest(Canonicalize(signedProperties))));
        signedInfo.AppendChild(Reference(document, null, $"#{keyInfoId}", null, Digest(Canonicalize(keyInfo))));

        signatureValue.InnerText = Convert.ToBase64String(
            rsa.SignData(Canonicalize(signedInfo), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            Base64FormattingOptions.None);
    }

    private static XmlElement KeyInfo(XmlDocument document, X509Certificate2 certificate, RSA rsa, string id)
    {
        var keyInfo = Element(document, "ds", "KeyInfo", DsNs);
        keyInfo.SetAttribute("Id", id);

        var x509Data = Element(document, "ds", "X509Data", DsNs);
        var x509Certificate = Element(document, "ds", "X509Certificate", DsNs);
        x509Certificate.InnerText = Convert.ToBase64String(certificate.RawData);
        x509Data.AppendChild(x509Certificate);
        keyInfo.AppendChild(x509Data);

        var parameters = rsa.ExportParameters(false);
        var rsaKeyValue = Element(document, "ds", "RSAKeyValue", DsNs);
        var modulus = Element(document, "ds", "Modulus", DsNs);
        modulus.InnerText = Convert.ToBase64String(parameters.Modulus!);
        var exponent = Element(document, "ds", "Exponent", DsNs);
        exponent.InnerText = Convert.ToBase64String(parameters.Exponent!);
        rsaKeyValue.AppendChild(modulus);
        rsaKeyValue.AppendChild(exponent);
        var keyValue = Element(document, "ds", "KeyValue", DsNs);
        keyValue.AppendChild(rsaKeyValue);
        keyInfo.AppendChild(keyValue);

        return keyInfo;
    }

    private static XmlElement SignedProperties(
        XmlDocument document, X509Certificate2 certificate, string id, string documentReferenceId, DateTime signingTimeUtc)
    {
        var signedProperties = Element(document, "xades", "SignedProperties", XadesNs);
        signedProperties.SetAttribute("Id", id);

        var signatureProperties = Element(document, "xades", "SignedSignatureProperties", XadesNs);
        signedProperties.AppendChild(signatureProperties);

        signatureProperties.AppendChild(Text(document, "xades", "SigningTime", XadesNs,
            signingTimeUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)));

        // The certificate the signature was made with
        var certDigest = Element(document, "xades", "CertDigest", XadesNs);
        certDigest.AppendChild(Element(document, "ds", "DigestMethod", DsNs, ("Algorithm", Sha256)));
        certDigest.AppendChild(Text(document, "ds", "DigestValue", DsNs, Convert.ToBase64String(SHA256.HashData(certificate.RawData))));
        var issuerSerial = Element(document, "xades", "IssuerSerial", XadesNs);
        issuerSerial.AppendChild(Text(document, "ds", "X509IssuerName", DsNs, certificate.IssuerName.Name));
        issuerSerial.AppendChild(Text(document, "ds", "X509SerialNumber", DsNs,
            new System.Numerics.BigInteger(certificate.GetSerialNumber(), isUnsigned: true, isBigEndian: true).ToString(CultureInfo.InvariantCulture)));
        var cert = Element(document, "xades", "Cert", XadesNs);
        cert.AppendChild(certDigest);
        cert.AppendChild(issuerSerial);
        var signingCertificate = Element(document, "xades", "SigningCertificate", XadesNs);
        signingCertificate.AppendChild(cert);
        signatureProperties.AppendChild(signingCertificate);

        // The Facturae signature policy
        var policyId = Element(document, "xades", "SigPolicyId", XadesNs);
        policyId.AppendChild(Text(document, "xades", "Identifier", XadesNs, PolicyUrl));
        policyId.AppendChild(Text(document, "xades", "Description", XadesNs, PolicyDescription));
        var policyHash = Element(document, "xades", "SigPolicyHash", XadesNs);
        policyHash.AppendChild(Element(document, "ds", "DigestMethod", DsNs, ("Algorithm", Sha1)));
        policyHash.AppendChild(Text(document, "ds", "DigestValue", DsNs, PolicyDigestSha1));
        var signaturePolicyId = Element(document, "xades", "SignaturePolicyId", XadesNs);
        signaturePolicyId.AppendChild(policyId);
        signaturePolicyId.AppendChild(policyHash);
        var policyIdentifier = Element(document, "xades", "SignaturePolicyIdentifier", XadesNs);
        policyIdentifier.AppendChild(signaturePolicyId);
        signatureProperties.AppendChild(policyIdentifier);

        // Signed by the issuer of the invoice
        var claimedRoles = Element(document, "xades", "ClaimedRoles", XadesNs);
        claimedRoles.AppendChild(Text(document, "xades", "ClaimedRole", XadesNs, "emisor"));
        var signerRole = Element(document, "xades", "SignerRole", XadesNs);
        signerRole.AppendChild(claimedRoles);
        signatureProperties.AppendChild(signerRole);

        var objectFormat = Element(document, "xades", "DataObjectFormat", XadesNs);
        objectFormat.SetAttribute("ObjectReference", $"#{documentReferenceId}");
        objectFormat.AppendChild(Text(document, "xades", "Description", XadesNs, "Factura electrónica"));
        objectFormat.AppendChild(Text(document, "xades", "MimeType", XadesNs, "text/xml"));
        var dataObjectProperties = Element(document, "xades", "SignedDataObjectProperties", XadesNs);
        dataObjectProperties.AppendChild(objectFormat);
        signedProperties.AppendChild(dataObjectProperties);

        return signedProperties;
    }

    private static XmlElement Reference(XmlDocument document, string? id, string uri, string? type, string digestValue, bool enveloped = false)
    {
        var reference = Element(document, "ds", "Reference", DsNs);
        if (id is not null)
            reference.SetAttribute("Id", id);
        reference.SetAttribute("URI", uri);
        if (type is not null)
            reference.SetAttribute("Type", type);

        if (enveloped)
        {
            var transforms = Element(document, "ds", "Transforms", DsNs);
            transforms.AppendChild(Element(document, "ds", "Transform", DsNs, ("Algorithm", EnvelopedTransform)));
            reference.AppendChild(transforms);
        }

        reference.AppendChild(Element(document, "ds", "DigestMethod", DsNs, ("Algorithm", Sha256)));
        reference.AppendChild(Text(document, "ds", "DigestValue", DsNs, digestValue));
        return reference;
    }

    private static string Digest(byte[] canonical) => Convert.ToBase64String(SHA256.HashData(canonical));

    /// <summary>Canonical form of a whole document.</summary>
    private static byte[] Canonicalize(XmlDocument document)
    {
        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(document);
        return ReadAll((Stream)transform.GetOutput(typeof(Stream)));
    }

    /// <summary>
    /// Canonical form of an element as it sits in its document: the element on its own, plus the
    /// namespace declarations it inherits, which inclusive C14N writes on the top element.
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
        }

        foreach (var (prefix, uri) in inherited)
        {
            var name = prefix.Length == 0 ? "xmlns" : $"xmlns:{prefix}";
            if (!copy.HasAttribute(name))
                copy.SetAttribute(name, uri);
        }

        return Canonicalize(standalone);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static XmlElement Element(XmlDocument document, string prefix, string name, string ns, params (string Name, string Value)[] attributes)
    {
        var element = document.CreateElement(prefix, name, ns);
        foreach (var (attributeName, value) in attributes)
            element.SetAttribute(attributeName, value);
        return element;
    }

    private static XmlElement Text(XmlDocument document, string prefix, string name, string ns, string text)
    {
        var element = Element(document, prefix, name, ns);
        element.InnerText = text;
        return element;
    }
}
