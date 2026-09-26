using Microsoft.AspNetCore.DataProtection;
using nInvoices.Application.Services.Email;

namespace nInvoices.Infrastructure.Gmail;

/// <summary>
/// Encrypts secrets with ASP.NET Data Protection. The keys live outside the database
/// (DataProtection:KeysPath), so a database backup alone cannot reveal the secrets.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "nInvoices.Gmail.RefreshToken";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
