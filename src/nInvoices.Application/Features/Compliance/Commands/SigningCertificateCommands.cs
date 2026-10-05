using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mediator;
using nInvoices.Application.Compliance;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Compliance.Commands;

/// <summary>
/// Stores the user's signing certificate for a country. The file and its password are encrypted
/// before they are saved. Result: the country, or null if the installation does not offer it.
/// </summary>
public sealed record SetSigningCertificateCommand(string CountryCode, UploadCertificateDto Certificate) : IRequest<ComplianceCountryDto?>;

/// <summary>Removes the stored signing certificate. Result: the country, or null if not offered.</summary>
public sealed record RemoveSigningCertificateCommand(string CountryCode) : IRequest<ComplianceCountryDto?>;

public sealed class SetSigningCertificateCommandHandler : IRequestHandler<SetSigningCertificateCommand, ComplianceCountryDto?>
{
    public const int MaxBytes = 256 * 1024;

    private readonly IComplianceRegistry _registry;
    private readonly IRepository<ComplianceSettings> _settings;
    private readonly ISecretProtector _protector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public SetSigningCertificateCommandHandler(
        IComplianceRegistry registry,
        IRepository<ComplianceSettings> settings,
        ISecretProtector protector,
        IUnitOfWork unitOfWork,
        TimeProvider time)
    {
        _registry = registry;
        _settings = settings;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    /// <exception cref="ArgumentException">Not a usable certificate: wrong password, no RSA private key, or expired.</exception>
    public async ValueTask<ComplianceCountryDto?> Handle(SetSigningCertificateCommand request, CancellationToken cancellationToken)
    {
        var module = _registry.Find(request.CountryCode);
        if (module is null)
            return null;

        byte[] pfx;
        try
        {
            pfx = Convert.FromBase64String(request.Certificate.Pfx);
        }
        catch (FormatException)
        {
            throw new ArgumentException("The certificate file could not be read");
        }

        if (pfx.Length == 0 || pfx.Length > MaxBytes)
            throw new ArgumentException("The certificate file is empty or too large");

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(pfx, request.Certificate.Password);
        }
        catch (CryptographicException)
        {
            throw new ArgumentException("The password is wrong, or the file is not a .p12/.pfx certificate");
        }

        using (certificate)
        {
            if (!certificate.HasPrivateKey || certificate.GetRSAPrivateKey() is null)
                throw new ArgumentException("The certificate has no RSA private key to sign with");
            if (certificate.NotAfter.ToUniversalTime() < _time.GetUtcNow().UtcDateTime)
                throw new ArgumentException($"The certificate expired on {certificate.NotAfter:yyyy-MM-dd}");

            // The query filter returns only the current user's rows
            var settings = (await _settings.GetAllAsync(cancellationToken))
                .FirstOrDefault(s => string.Equals(s.CountryCode, module.CountryCode, StringComparison.OrdinalIgnoreCase));
            var isNew = settings is null;
            settings ??= new ComplianceSettings(module.CountryCode);

            settings.SetCertificate(
                _protector.Protect(Convert.ToBase64String(pfx)),
                _protector.Protect(request.Certificate.Password),
                certificate.Subject,
                certificate.Thumbprint,
                certificate.NotAfter.ToUniversalTime());

            if (isNew)
                await _settings.AddAsync(settings, cancellationToken);
            else
                await _settings.UpdateAsync(settings, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ComplianceMapper.ToDto(module, settings, _registry.RequiresSigningCertificate(module.CountryCode));
        }
    }
}

public sealed class RemoveSigningCertificateCommandHandler : IRequestHandler<RemoveSigningCertificateCommand, ComplianceCountryDto?>
{
    private readonly IComplianceRegistry _registry;
    private readonly IRepository<ComplianceSettings> _settings;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveSigningCertificateCommandHandler(
        IComplianceRegistry registry,
        IRepository<ComplianceSettings> settings,
        IUnitOfWork unitOfWork)
    {
        _registry = registry;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<ComplianceCountryDto?> Handle(RemoveSigningCertificateCommand request, CancellationToken cancellationToken)
    {
        var module = _registry.Find(request.CountryCode);
        if (module is null)
            return null;

        var settings = (await _settings.GetAllAsync(cancellationToken))
            .FirstOrDefault(s => string.Equals(s.CountryCode, module.CountryCode, StringComparison.OrdinalIgnoreCase));

        if (settings is { HasCertificate: true })
        {
            settings.ClearCertificate();
            await _settings.UpdateAsync(settings, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ComplianceMapper.ToDto(module, settings, _registry.RequiresSigningCertificate(module.CountryCode));
    }
}
