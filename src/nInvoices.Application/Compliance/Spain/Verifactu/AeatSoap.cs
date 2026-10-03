using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>Which AEAT service a submission goes to: the test portal or production, and for a personal or a company-seal certificate.</summary>
public sealed record AeatTarget(bool Production, bool SealCertificate)
{
    /// <summary>The SOAP address of the Verifactu service (SistemaFacturacion.wsdl).</summary>
    public string Url => (Production, SealCertificate) switch
    {
        (true, false) => "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (true, true) => "https://www10.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (false, false) => "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP",
        (false, true) => "https://prewww10.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP"
    };
}

/// <param name="InvoiceNumber">NumSerieFactura of the record the line answers.</param>
/// <param name="Operation">"Alta" or "Anulacion".</param>
/// <param name="Status">Correcto, AceptadoConErrores or Incorrecto.</param>
public sealed record AeatLineResult(string InvoiceNumber, string Operation, string Status, string? ErrorCode, string? ErrorDescription);

/// <param name="Csv">Secure verification code of the submission.</param>
/// <param name="WaitSeconds">The pause the Tax Agency asks for before the next submission.</param>
/// <param name="EnvelopeStatus">Correcto, ParcialmenteCorrecto or Incorrecto.</param>
public sealed record AeatResponse(string? Csv, int WaitSeconds, string EnvelopeStatus, IReadOnlyList<AeatLineResult> Lines);

/// <summary>A submission got no usable answer: the network, the certificate, or a SOAP fault.</summary>
public sealed class AeatException : Exception
{
    public AeatException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Sends a batch of records to the Tax Agency over TLS authenticated with the user certificate.</summary>
public interface IAeatVerifactuClient
{
    /// <exception cref="AeatException">There was no usable answer.</exception>
    Task<AeatResponse> SendAsync(AeatTarget target, X509Certificate2 certificate, XElement batch, CancellationToken cancellationToken = default);
}

/// <summary>The SOAP 1.1 messages of the Verifactu service (document style, empty SOAPAction).</summary>
public static class AeatSoap
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private static readonly XNamespace Response =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    public static XDocument Envelope(XElement batch) =>
        new(new XDeclaration("1.0", "UTF-8", null),
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
                new XElement(Soap + "Header"),
                new XElement(Soap + "Body", batch)));

    /// <exception cref="AeatException">It is a SOAP fault, or not the answer to a submission.</exception>
    public static AeatResponse ParseResponse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new AeatException("The Tax Agency answered with something that is not XML", ex);
        }

        var body = document.Root?.Element(Soap + "Body")
            ?? throw new AeatException("The Tax Agency answer has no SOAP body");

        if (body.Element(Soap + "Fault") is { } fault)
        {
            var code = fault.Element("faultcode")?.Value;
            var text = fault.Element("faultstring")?.Value;
            throw new AeatException($"The Tax Agency refused the request: {text ?? code ?? "SOAP fault"}");
        }

        var answer = body.Element(Response + "RespuestaRegFactuSistemaFacturacion")
            ?? throw new AeatException("The Tax Agency answer is not the answer to a submission");

        var sf = VerifactuXml.Sf;
        var lines = answer.Elements(Response + "RespuestaLinea")
            .Select(line => new AeatLineResult(
                line.Element(Response + "IDFactura")?.Element(sf + "NumSerieFactura")?.Value ?? "",
                line.Element(Response + "Operacion")?.Element(sf + "TipoOperacion")?.Value ?? "",
                line.Element(Response + "EstadoRegistro")?.Value ?? "",
                line.Element(Response + "CodigoErrorRegistro")?.Value,
                line.Element(Response + "DescripcionErrorRegistro")?.Value))
            .ToList();

        var wait = int.TryParse(answer.Element(Response + "TiempoEsperaEnvio")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 60;

        return new AeatResponse(
            answer.Element(Response + "CSV")?.Value,
            wait,
            answer.Element(Response + "EstadoEnvio")?.Value ?? "",
            lines);
    }
}
