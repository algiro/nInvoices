using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance;

/// <summary>
/// Opens the signing certificate a user stored (kept encrypted), for whatever needs it: signing an
/// e-invoice, or authenticating with a tax authority.
/// </summary>
public sealed class SigningCertificateLoader
{
    private readonly ISecretProtector _protector;
    private readonly TimeProvider _time;

    public SigningCertificateLoader(ISecretProtector protector, TimeProvider time)
    {
        _protector = protector;
        _time = time;
    }

    /// <summary>
    /// The certificate with its private key, or null with the reason added to <paramref name="issues"/>.
    /// The caller disposes it.
    /// </summary>
    public X509Certificate2? Load(ComplianceSettings settings, List<ComplianceIssue> issues)
    {
        if (!settings.HasCertificate)
        {
            issues.Add(new ComplianceIssue(null, "Upload your signing certificate in Settings"));
            return null;
        }

        if (settings.CertificateNotAfter is { } notAfter && notAfter < _time.GetUtcNow().UtcDateTime)
        {
            issues.Add(new ComplianceIssue(null, $"The signing certificate expired on {notAfter:yyyy-MM-dd}"));
            return null;
        }

        try
        {
            var pfx = Convert.FromBase64String(_protector.Unprotect(settings.ProtectedCertificate!));
            return X509CertificateLoader.LoadPkcs12(pfx, _protector.Unprotect(settings.ProtectedCertificatePassword!));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            issues.Add(new ComplianceIssue(null, "The stored signing certificate can no longer be read: upload it again"));
            return null;
        }
    }
}
