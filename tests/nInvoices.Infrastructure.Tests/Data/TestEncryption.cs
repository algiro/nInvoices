using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Tests.Data;

/// <summary>
/// One encryptor (random master key, keys in memory) shared by the DbContext tests, so they all use
/// the same EF model and exercise real encryption.
/// </summary>
internal static class TestEncryption
{
    public static FieldEncryptor Encryptor { get; } = FieldEncryptor.CreateEphemeral();
}
