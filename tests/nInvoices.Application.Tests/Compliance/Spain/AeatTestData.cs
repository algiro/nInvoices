using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace nInvoices.Application.Tests.Compliance.Spain;

/// <summary>Answers from the Tax Agency web service, built to the shape of its response schema (and checked against it).</summary>
internal static class AeatTestData
{
    private const string Tik = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
    private const string TikR = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    /// <param name="lines">Number, operation (Alta/Anulacion), status, and optionally the error code and description.</param>
    public static string Answer(
        string envelopeStatus, int wait, string? csv, params (string Number, string Operation, string Status, string? Code, string? Description)[] lines)
    {
        var rows = string.Concat(lines.Select(l => $@"
        <tikR:RespuestaLinea>
          <tikR:IDFactura>
            <tik:IDEmisorFactura>12345678Z</tik:IDEmisorFactura>
            <tik:NumSerieFactura>{l.Number}</tik:NumSerieFactura>
            <tik:FechaExpedicionFactura>03-10-2026</tik:FechaExpedicionFactura>
          </tikR:IDFactura>
          <tikR:Operacion><tik:TipoOperacion>{l.Operation}</tik:TipoOperacion></tikR:Operacion>
          <tikR:EstadoRegistro>{l.Status}</tikR:EstadoRegistro>
          {(l.Code is null ? "" : $"<tikR:CodigoErrorRegistro>{l.Code}</tikR:CodigoErrorRegistro>")}
          {(l.Description is null ? "" : $"<tikR:DescripcionErrorRegistro>{l.Description}</tikR:DescripcionErrorRegistro>")}
        </tikR:RespuestaLinea>"));

        return $@"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/"">
  <env:Header/>
  <env:Body>
    <tikR:RespuestaRegFactuSistemaFacturacion xmlns:tikR=""{TikR}"" xmlns:tik=""{Tik}"">
      {(csv is null ? "" : $"<tikR:CSV>{csv}</tikR:CSV>")}
      <tikR:DatosPresentacion>
        <tik:NIFPresentador>12345678Z</tik:NIFPresentador>
        <tik:TimestampPresentacion>2026-10-03T11:30:05+02:00</tik:TimestampPresentacion>
      </tikR:DatosPresentacion>
      <tikR:Cabecera>
        <tik:ObligadoEmision><tik:NombreRazon>Ana Perez</tik:NombreRazon><tik:NIF>12345678Z</tik:NIF></tik:ObligadoEmision>
      </tikR:Cabecera>
      <tikR:TiempoEsperaEnvio>{wait}</tikR:TiempoEsperaEnvio>
      <tikR:EstadoEnvio>{envelopeStatus}</tikR:EstadoEnvio>{rows}
    </tikR:RespuestaRegFactuSistemaFacturacion>
  </env:Body>
</env:Envelope>";
    }

    public const string Fault = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header/><env:Body>
  <env:Fault><faultcode>env:Client</faultcode><faultstring>Codigo[4102].El XML no cumple el esquema.</faultstring></env:Fault>
</env:Body></env:Envelope>";

    private static readonly Lazy<XmlSchemaSet> Schemas = new(() =>
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Compliance", "Spain", "Schemas");
        var set = new XmlSchemaSet { XmlResolver = null };
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        foreach (var file in new[] { "xmldsig-core-schema.xsd", "SuministroInformacion.xsd", "SuministroLR.xsd", "RespuestaSuministro.xsd" })
        {
            using var reader = XmlReader.Create(Path.Combine(directory, file), settings);
            set.Add(null, reader);
        }

        set.Compile();
        return set;
    });

    /// <summary>The schema violations of the answer body (the SOAP envelope is not part of the schema).</summary>
    public static List<string> SchemaErrors(string answer)
    {
        var body = XDocument.Parse(answer).Root!.Elements().First(e => e.Name.LocalName == "Body").Elements().First();
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Schemas.Value, XmlResolver = null };
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        using var reader = XmlReader.Create(new StringReader(body.ToString(SaveOptions.DisableFormatting)), settings);
        while (reader.Read()) { }
        return errors;
    }
}
