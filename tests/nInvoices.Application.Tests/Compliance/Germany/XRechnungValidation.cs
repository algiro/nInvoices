using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace nInvoices.Application.Tests.Compliance.Germany;

/// <summary>
/// The XRechnung checks a recipient makes: the UBL 2.1 schema, then the EN 16931 and XRechnung Schematron rules
/// through KoSIT's validator. Both come from KoSIT's XRechnung 3.0.2 validator configuration (2026-08-31), under
/// <c>Schemas/</c> unchanged but for the UBL documents other than Invoice and CreditNote, the docs and the copies of
/// the EN 16931 XSLs at its root; its UBL schemas are byte for byte OASIS's runtime ones (os-UBL-2.1/xsdrt).
/// </summary>
internal static class XRechnungValidation
{
    public const string ValidatorJarName = "validator-1.6.3-standalone.jar";

    /// <summary>The validator jar to use; when unset, the one get-kosit-validator.ps1 fetched into <c>Validator/</c>.</summary>
    public const string ValidatorJarEnvironmentVariable = "KOSIT_VALIDATOR_JAR";

    /// <summary>Set (as in CI) to fail instead of skipping when Java or the validator jar is missing.</summary>
    public const string RequireValidatorEnvironmentVariable = "REQUIRE_KOSIT_VALIDATOR";

    public static string Configuration { get; } = Path.Combine(AppContext.BaseDirectory, "Compliance", "Germany", "Schemas");

    private static readonly Lazy<XmlSchemaSet> Schemas = new(() =>
    {
        // The resolver only follows the relative imports between the local schema files
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        var xsd = Path.Combine(Configuration, "resources", "ubl", "2.1", "xsd");

        // The XML Signature schema has a DOCTYPE (internal only), which an import would refuse to read: add it first
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        foreach (var file in new[] { Path.Combine("common", "UBL-xmldsig-core-schema-2.1.xsd"), Path.Combine("maindoc", "UBL-Invoice-2.1.xsd") })
        {
            using var reader = XmlReader.Create(Path.Combine(xsd, file), settings);
            set.Add(null, reader);
        }

        set.Compile();
        return set;
    });

    /// <returns>Every UBL 2.1 schema violation of the invoice; empty when it is valid.</returns>
    public static List<string> SchemaErrors(byte[] invoice)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Schemas.Value, XmlResolver = null };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message} (line {e.Exception?.LineNumber})");

        using var reader = XmlReader.Create(new MemoryStream(invoice), settings);
        while (reader.Read()) { }
        return errors;
    }

    /// <returns>The validator jar, or null (with why) when it can't run here.</returns>
    public static string? ValidatorJar(out string why)
    {
        var jar = Environment.GetEnvironmentVariable(ValidatorJarEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(jar))
            jar = Path.Combine(AppContext.BaseDirectory, "Compliance", "Germany", "Validator", ValidatorJarName);
        if (!File.Exists(jar))
        {
            why = $"KoSIT validator not found at {jar}: run tests/nInvoices.Application.Tests/Compliance/Germany/get-kosit-validator.ps1";
            return null;
        }
        if (!JavaIsAvailable())
        {
            why = "Java is not on the PATH: the KoSIT validator needs Java 11 or later";
            return null;
        }
        why = string.Empty;
        return jar;
    }

    /// <summary>
    /// Runs the KoSIT validator once over the invoices (one JVM start for all of them).
    /// </summary>
    /// <returns>Per invoice name: the error and fatal messages of the report, plus "rejected" when it wasn't accepted.</returns>
    public static async Task<Dictionary<string, List<string>>> SchematronErrorsAsync(
        string jar, IReadOnlyDictionary<string, byte[]> invoices, CancellationToken cancellationToken)
    {
        var work = Directory.CreateTempSubdirectory("xrechnung-");
        try
        {
            var files = new List<string>();
            foreach (var (name, content) in invoices)
            {
                var file = Path.Combine(work.FullName, $"{name}.xml");
                await File.WriteAllBytesAsync(file, content, cancellationToken);
                files.Add(file);
            }

            // Standard input is an empty pipe: the validator reads a document from it when piped, and fails when it's NUL
            var start = new ProcessStartInfo("java") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-jar", jar, "-s", Path.Combine(Configuration, "scenarios.xml"), "-r", Configuration, "-o", work.FullName })
                start.ArgumentList.Add(argument);
            files.ForEach(start.ArgumentList.Add);

            using var java = Process.Start(start) ?? throw new InvalidOperationException("java did not start");
            java.StandardInput.Close();
            var output = java.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = java.StandardError.ReadToEndAsync(cancellationToken);
            await java.WaitForExitAsync(cancellationToken);
            var log = $"{await output}{await error}";

            var results = new Dictionary<string, List<string>>();
            foreach (var name in invoices.Keys)
            {
                var report = Path.Combine(work.FullName, $"{name}-report.xml");
                results[name] = File.Exists(report)
                    ? ReportErrors(XDocument.Load(report))
                    : [$"no report (exit code {java.ExitCode}): {log}"];
            }
            return results;
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static readonly XNamespace Report = "http://www.xoev.de/de/validator/varl/1";

    // The report lists every schema and Schematron finding as a message; warnings and information don't count
    private static List<string> ReportErrors(XDocument report)
    {
        var errors = report.Descendants(Report + "message")
            .Where(m => (string?)m.Attribute("level") is "error" or "fatal")
            .Select(m => $"{(string?)m.Attribute("code")}: {m.Value.Trim()} ({(string?)m.Attribute("xpathLocation")})")
            .ToList();
        if (!report.Descendants(Report + "accept").Any())
            errors.Add("rejected");
        return errors;
    }

    private static bool JavaIsAvailable()
    {
        try
        {
            using var java = Process.Start(new ProcessStartInfo("java", "-version") { RedirectStandardError = true, RedirectStandardOutput = true });
            if (java is null)
                return false;
            java.WaitForExit(30_000);
            return java.HasExited && java.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
