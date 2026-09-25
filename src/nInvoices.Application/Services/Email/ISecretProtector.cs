namespace nInvoices.Application.Services.Email;

/// <summary>
/// Encrypts secrets (such as OAuth refresh tokens) before they are stored in the database.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The value was not produced by this protector or the key that encrypted it is gone.
    /// </exception>
    string Unprotect(string ciphertext);
}
