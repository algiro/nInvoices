using System.Globalization;
using System.Xml.Linq;

namespace nInvoices.Application.Compliance.Spain.Verifactu;

/// <summary>
/// Builds the Verifactu records as the XML AEAT defines (SuministroInformacion.xsd v1.0): the record
/// of an issued invoice (RegistroAlta), the record of a cancelled one (RegistroAnulacion), and the
/// batch that wraps them for submission (RegFactuSistemaFacturacion). Elements follow the schema order.
/// </summary>
public static class VerifactuXml
{
    public static readonly XNamespace Sf =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

    public static readonly XNamespace Lr =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <param name="previous">The record before; null for the first record of the chain.</param>
    /// <param name="generatedAt">FechaHoraHusoGenRegistro as hashed.</param>
    public static XElement Issued(
        VerifactuInvoiceData invoice, VerifactuSystem system, VerifactuPrevious? previous, string generatedAt, string hash)
    {
        return new XElement(Sf + "RegistroAlta",
            Sf.E("IDVersion", "1.0"),
            new XElement(Sf + "IDFactura",
                Sf.E("IDEmisorFactura", invoice.IssuerTaxId),
                Sf.E("NumSerieFactura", invoice.InvoiceNumber),
                Sf.E("FechaExpedicionFactura", VerifactuHash.Date(invoice.IssueDate))),
            Sf.E("NombreRazonEmisor", Truncate(invoice.IssuerName, 120)),
            Sf.E("TipoFactura", invoice.InvoiceType),
            Sf.E("DescripcionOperacion", Truncate(invoice.Description, 500)),
            new XElement(Sf + "Destinatarios", new XElement(Sf + "IDDestinatario", Recipient(invoice.Recipient))),
            new XElement(Sf + "Desglose", invoice.Breakdown.Select(Detail)),
            Sf.E("CuotaTotal", VerifactuHash.Amount(invoice.TotalTax)),
            Sf.E("ImporteTotal", VerifactuHash.Amount(invoice.TotalAmount)),
            Chaining(previous),
            System(system),
            Sf.E("FechaHoraHusoGenRegistro", generatedAt),
            Sf.E("TipoHuella", "01"),
            Sf.E("Huella", hash));
    }

    public static XElement Cancelled(
        string issuerTaxId, string invoiceNumber, DateOnly issueDate,
        VerifactuSystem system, VerifactuPrevious? previous, string generatedAt, string hash)
    {
        return new XElement(Sf + "RegistroAnulacion",
            Sf.E("IDVersion", "1.0"),
            new XElement(Sf + "IDFactura",
                Sf.E("IDEmisorFacturaAnulada", issuerTaxId),
                Sf.E("NumSerieFacturaAnulada", invoiceNumber),
                Sf.E("FechaExpedicionFacturaAnulada", VerifactuHash.Date(issueDate))),
            Chaining(previous),
            System(system),
            Sf.E("FechaHoraHusoGenRegistro", generatedAt),
            Sf.E("TipoHuella", "01"),
            Sf.E("Huella", hash));
    }

    /// <summary>The batch sent to AEAT: the issuer and its records, which must be in chain order.</summary>
    public static XElement Batch(string issuerName, string issuerTaxId, IEnumerable<XElement> records)
    {
        return new XElement(Lr + "RegFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "lr", Lr.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sf", Sf.NamespaceName),
            new XElement(Lr + "Cabecera",
                new XElement(Sf + "ObligadoEmision",
                    Sf.E("NombreRazon", Truncate(issuerName, 120)),
                    Sf.E("NIF", issuerTaxId))),
            records.Select(r => new XElement(Lr + "RegistroFactura", r)));
    }

    private static XElement Chaining(VerifactuPrevious? previous) =>
        new(Sf + "Encadenamiento",
            previous is null
                ? Sf.E("PrimerRegistro", "S")
                : new XElement(Sf + "RegistroAnterior",
                    Sf.E("IDEmisorFactura", previous.IssuerTaxId),
                    Sf.E("NumSerieFactura", previous.InvoiceNumber),
                    Sf.E("FechaExpedicionFactura", previous.IssueDate),
                    Sf.E("Huella", previous.Hash)));

    private static XElement System(VerifactuSystem system) =>
        new(Sf + "SistemaInformatico",
            Sf.E("NombreRazon", Truncate(system.ProducerName, 120)),
            Sf.E("NIF", system.ProducerTaxId),
            Sf.E("NombreSistemaInformatico", Truncate(system.SystemName, 30)),
            Sf.E("IdSistemaInformatico", system.SystemId),
            Sf.E("Version", Truncate(system.Version, 50)),
            Sf.E("NumeroInstalacion", Truncate(system.InstallationNumber, 100)),
            Sf.E("TipoUsoPosibleSoloVerifactu", "S"),
            Sf.E("TipoUsoPosibleMultiOT", "S"),
            Sf.E("IndicadorMultiplesOT", system.MultipleTaxpayers ? "S" : "N"));

    private static IEnumerable<XElement> Recipient(VerifactuRecipient recipient)
    {
        yield return Sf.E("NombreRazon", Truncate(recipient.Name, 120));

        if (recipient.Nif is not null)
        {
            yield return Sf.E("NIF", recipient.Nif);
            yield break;
        }

        yield return new XElement(Sf + "IDOtro",
            Sf.E("CodigoPais", recipient.CountryCode!),
            Sf.E("IDType", recipient.IdType!),
            Sf.E("ID", Truncate(recipient.Id!, 20)));
    }

    private static XElement Detail(VerifactuBreakdown line)
    {
        var detail = new XElement(Sf + "DetalleDesglose", Sf.E("ClaveRegimen", "01"));

        // S1 is taxed; S2 (reverse charge), N1/N2 (not subject) and E1..E6 (exempt) carry no tax
        if (line.Operation is "S1" or "S2" or "N1" or "N2")
            detail.Add(Sf.E("CalificacionOperacion", line.Operation));
        else
            detail.Add(Sf.E("OperacionExenta", line.Operation));

        if (line.Operation == "S1")
            detail.Add(Sf.E("TipoImpositivo", line.Rate.ToString("0.00", Invariant)));

        detail.Add(Sf.E("BaseImponibleOimporteNoSujeto", VerifactuHash.Amount(line.Base)));

        if (line.Operation == "S1")
            detail.Add(Sf.E("CuotaRepercutida", VerifactuHash.Amount(line.Tax)));

        return detail;
    }

    private static XElement E(this XNamespace ns, string name, string value) => new(ns + name, value);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
