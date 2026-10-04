using System.Security.Cryptography;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>
/// The server's master key (and, while rotating, the ones it replaced). It never encrypts data
/// itself, only the per-user keys. It is read from a file kept outside the database and its
/// backups: losing it makes every user's data unreadable.
/// </summary>
public sealed class MasterKeyRing
{
    public const int KeySize = 32;

    private readonly Dictionary<string, byte[]> _keys;

    private MasterKeyRing(byte[] current, IEnumerable<byte[]> previous)
    {
        CurrentId = IdOf(current);
        Current = current;
        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [CurrentId] = current };
        foreach (var key in previous)
            _keys.TryAdd(IdOf(key), key);
    }

    /// <summary>A fingerprint of the current key, stored next to each user key it wraps.</summary>
    public string CurrentId { get; }

    public byte[] Current { get; }

    public static MasterKeyRing FromKeys(byte[] current, params byte[][] previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);
        Validate(current, "current master key");
        foreach (var key in previous)
            Validate(key, "previous master key");
        return new MasterKeyRing(current, previous);
    }

    /// <summary>
    /// Loads the key file (one line: 32 random bytes in base64) and any previous key files.
    /// With <paramref name="createIfMissing"/> a missing current file is generated, which is only
    /// meant for development: in production a missing key must stop the app, not start it with a
    /// new key that can't read the existing data.
    /// </summary>
    public static MasterKeyRing Load(string keyFile, IEnumerable<string> previousKeyFiles, bool createIfMissing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFile);
        ArgumentNullException.ThrowIfNull(previousKeyFiles);

        if (!File.Exists(keyFile))
        {
            if (!createIfMissing)
                throw new InvalidOperationException(
                    $"Encryption master key file '{keyFile}' not found. Restore it from your backup (see Docs/ENCRYPTION.md); " +
                    "generating a new one would leave the existing data unreadable.");
            CreateKeyFile(keyFile);
        }

        return FromKeys(ReadKeyFile(keyFile), previousKeyFiles.Select(ReadKeyFile).ToArray());
    }

    public static string NewKeyText() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySize));

    public bool TryGet(string id, out byte[] key) => _keys.TryGetValue(id, out key!);

    private static string IdOf(byte[] key) => Convert.ToHexString(SHA256.HashData(key), 0, 8).ToLowerInvariant();

    private static byte[] ReadKeyFile(string path)
    {
        var text = File.ReadAllText(path).Trim();
        var key = new byte[KeySize];
        if (!Convert.TryFromBase64String(text, key, out var written) || written != KeySize)
            throw new InvalidOperationException($"Encryption master key file '{path}' must hold {KeySize} bytes in base64.");
        return key;
    }

    private static void CreateKeyFile(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, NewKeyText() + Environment.NewLine);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void Validate(byte[] key, string name)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
            throw new ArgumentException($"The {name} must be {KeySize} bytes.", nameof(key));
    }
}
