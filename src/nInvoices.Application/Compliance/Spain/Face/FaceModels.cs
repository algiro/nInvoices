using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace nInvoices.Application.Compliance.Spain.Face;

/// <summary>Which FACe service a request goes to: the staging (test) environment or production.</summary>
public sealed record FaceTarget(bool Production)
{
    /// <summary>The address of the providers service (proveedores/v1/factura).</summary>
    public string Url => Production
        ? "https://ws.face.gob.es/proveedores/v1/factura"
        : "https://se-ws-face.redsara.es/proveedores/v1/factura";

    /// <summary>The namespace of the operations: the WSDL's target namespace, which is the service address.</summary>
    public string Namespace => Url;
}

/// <summary>An invoice as FACe describes it once it has registered it.</summary>
public sealed record FaceInvoice(
    string RegistryCode,
    DateTime? RegisteredAt,
    string? Series,
    string? Number,
    string? StatusCode,
    string? StatusName,
    string? CancellationCode,
    string? CancellationName,
    string? ReceiverName,
    string? ManagingBody,
    string? ProcessingUnit,
    string? AccountingOffice);

/// <summary>FACe refused the request, or could not be reached.</summary>
public sealed class FaceException : Exception
{
    public FaceException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Talks to FACe web service for providers, signing every request with the user certificate.</summary>
public interface IFaceClient
{
    /// <exception cref="FaceException">FACe refused the invoice or could not be reached.</exception>
    Task<FaceInvoice> SendInvoiceAsync(
        FaceTarget target, X509Certificate2 certificate, string notificationEmail, string fileName, byte[] xsig,
        CancellationToken cancellationToken = default);

    /// <exception cref="FaceException">FACe could not be asked, or does not know the invoice.</exception>
    Task<FaceInvoice> GetInvoiceAsync(
        FaceTarget target, X509Certificate2 certificate, string registryCode, CancellationToken cancellationToken = default);
}

/// <summary>The messages of FACe providers web service (WSDL proveedores/v1/factura: SOAP 1.1, RPC style, literal).</summary>
public static class FaceSoap
{

    public static string SoapAction(FaceTarget target, string operation) => $"\"{target.Namespace}#{operation}\"";

    /// <summary>The body of enviarFactura: the notification email and the signed invoice, in base64.</summary>
    public static XElement SendInvoice(FaceTarget target, string email, string fileName, byte[] xsig)
    {
        var ns = XNamespace.Get(target.Namespace);

        // RPC style: an element named after the operation holds one unqualified element per message part
        return new XElement(ns + "enviarFactura",
            new XAttribute(XNamespace.Xmlns + "ns", ns.NamespaceName),
            new XElement("request",
                new XElement("correo", email),
                new XElement("factura",
                    new XElement("factura", Convert.ToBase64String(xsig)),
                    new XElement("nombre", fileName))));
    }

    /// <summary>The body of detalleFactura: asks for the invoice with this registry code.</summary>
    public static XElement GetInvoice(FaceTarget target, string registryCode)
    {
        var ns = XNamespace.Get(target.Namespace);
        return new XElement(ns + "detalleFactura",
            new XAttribute(XNamespace.Xmlns + "ns", ns.NamespaceName),
            new XElement("request", new XElement("codigoRegistro", registryCode)));
    }

    /// <exception cref="FaceException">It is a SOAP fault, or not the answer to a request.</exception>
    public static FaceInvoice ParseInvoiceResponse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new FaceException("FACe answered with something that is not XML", ex);
        }

        var body = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body")
            ?? throw new FaceException("FACe answer has no SOAP body");

        if (body.Elements().FirstOrDefault(e => e.Name.LocalName == "Fault") is { } fault)
            throw new FaceException(DescribeFault(fault));

        // The answer wraps the invoice: <...Response><return><factura>...; tolerate the wrapper being absent
        var invoice = body.Descendants().FirstOrDefault(e => e.Name.LocalName == "factura" && e.Elements().Any(c => c.Name.LocalName == "registro"))
            ?? throw new FaceException("FACe answer does not describe an invoice");

        string? Text(XElement parent, params string[] path)
        {
            XElement? current = parent;
            foreach (var name in path)
                current = current?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            return string.IsNullOrWhiteSpace(current?.Value) ? null : current.Value.Trim();
        }

        var code = Text(invoice, "registro", "codigo")
            ?? throw new FaceException("FACe answer has no registry code");

        DateTime? registered = DateTimeOffset.TryParse(Text(invoice, "registro", "fecha"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : null;

        return new FaceInvoice(
            code,
            registered,
            Text(invoice, "serie"),
            Text(invoice, "numero"),
            Text(invoice, "estadoTramitacion", "codigo"),
            Text(invoice, "estadoTramitacion", "nombre"),
            Text(invoice, "estadoAnulacion", "codigo"),
            Text(invoice, "estadoAnulacion", "nombre"),
            Text(invoice, "receptor", "nombre"),
            Text(invoice, "relacion", "organoGestor", "codigo"),
            Text(invoice, "relacion", "unidadTramitadora", "codigo"),
            Text(invoice, "relacion", "oficinaContable", "codigo"));
    }

    private static string DescribeFault(XElement fault)
    {
        string? Child(XElement parent, string name) =>
            parent.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

        var text = Child(fault, "faultstring") ?? Child(fault, "faultcode") ?? "SOAP fault";
        var detail = fault.Elements().FirstOrDefault(e => e.Name.LocalName == "detail")?.Value.Trim();
        return string.IsNullOrWhiteSpace(detail) || text.Contains(detail, StringComparison.Ordinal)
            ? $"FACe refused the request: {text}"
            : $"FACe refused the request: {text} ({detail})";
    }
}
