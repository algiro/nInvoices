using System.Globalization;
using System.Text;
using QRCoder;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>
/// The QR code of a Verifactu invoice, as AEAT specifies it ("Detalle de las especificaciones técnicas
/// del código QR de la factura", v0.5.0): the URL of AEAT verification service with the issuer NIF,
/// the number, the date and the total, URL-encoded as UTF-8; drawn as ISO/IEC 18004 with level M
/// error correction. On paper it must measure between 30 and 40 mm, with 2 mm of white around it,
/// be headed "QR tributario:" and be followed by a verifiable-invoice legend.
/// </summary>
public static class VerifactuQr
{
    public const string TestBaseUrl = "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR";
    public const string ProductionBaseUrl = "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR";

    /// <summary>The text that must precede the QR code.</summary>
    public const string Heading = "QR tributario:";

    /// <summary>The phrase that must follow the QR code of an invoice issued by a Verifactu system.</summary>
    public const string Legend = "Factura verificable en la sede electrónica de la AEAT";

    /// <param name="issueDate">DD-MM-YYYY.</param>
    /// <param name="totalAmount">Total of the invoice, e.g. "241.40".</param>
    public static string Url(bool production, string issuerTaxId, string invoiceNumber, string issueDate, string totalAmount) =>
        $"{(production ? ProductionBaseUrl : TestBaseUrl)}?nif={Encode(issuerTaxId)}&numserie={Encode(invoiceNumber)}&fecha={Encode(issueDate)}&importe={Encode(totalAmount)}";

    /// <summary>The QR code as an SVG image, scalable to the size the invoice needs.</summary>
    public static string Svg(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        // Without a white border of its own: the invoice leaves at least 2 mm of white around it, as AEAT asks
        return new SvgQRCode(data).GetGraphic(viewBox: new System.Drawing.Size(200, 200), drawQuietZones: false);
    }

    /// <summary>The same image as a data URI, to use as an img source.</summary>
    public static string SvgDataUri(string url) =>
        "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(Svg(url)));

    /// <summary>Percent-encoding of UTF-8 bytes, as web applications do ("&amp;" becomes "%26").</summary>
    private static string Encode(string value) => Uri.EscapeDataString(value);
}
