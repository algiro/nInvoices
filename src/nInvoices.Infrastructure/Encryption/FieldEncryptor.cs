using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>
/// Encrypts and decrypts column values with AES-256-GCM, using one key per user (envelope
/// encryption: the user keys are themselves encrypted by the server's <see cref="MasterKeyRing"/>).
/// <para>
/// A stored value is <c>"enc1:" + base64(keyId | nonce | tag | ciphertext)</c> for text and
/// <c>"enc1" + keyId | nonce | tag | ciphertext</c> for binary columns. The key id lets any value be
/// decrypted without knowing whose it is; the column's purpose string is authenticated with it, so
/// a value copied into another column fails to decrypt.
/// </para>
/// <para>
/// Values written before encryption have no prefix and are returned as they are, so the data can
/// be encrypted in place after an upgrade (<see cref="LegacyDataEncryption"/>).
/// </para>
/// <para>
/// Encrypting needs to know whose key to use: <see cref="Data.ApplicationDbContext"/> opens a
/// <see cref="BeginWriting"/> scope around each save. Outside one, encrypting throws, which also
/// makes a query that compares an encrypted column with a value (it can never match, the nonce is
/// random) fail loudly instead of silently finding nothing.
/// </para>
/// </summary>
public sealed class FieldEncryptor
{
    public const string TextPrefix = "enc1:";

    private const int KeyIdSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = KeyIdSize + NonceSize + TagSize;
    private static readonly byte[] BinaryPrefix = "enc1"u8.ToArray();
    private static readonly byte[] WrapPurpose = "nInvoices.UserKey/v1"u8.ToArray();

    private static readonly AsyncLocal<string?> WritingOwner = new();
    private static readonly AsyncLocal<StrongBox<int>?> LegacyReads = new();

    private readonly MasterKeyRing _masterKeys;
    private readonly IUserKeyStore _store;
    private readonly ConcurrentDictionary<Guid, byte[]> _keysById = new();
    private readonly ConcurrentDictionary<string, Guid> _keyIdByOwner = new(StringComparer.Ordinal);

    public FieldEncryptor(MasterKeyRing masterKeys, IUserKeyStore store)
    {
        ArgumentNullException.ThrowIfNull(masterKeys);
        ArgumentNullException.ThrowIfNull(store);
        _masterKeys = masterKeys;
        _store = store;
    }

    /// <summary>A throwaway encryptor with a random master key and keys kept in memory, for tests.</summary>
    public static FieldEncryptor CreateEphemeral() =>
        new(MasterKeyRing.FromKeys(RandomNumberGenerator.GetBytes(MasterKeyRing.KeySize)), new InMemoryUserKeyStore());

    /// <summary>
    /// Loads (or creates, the first time) <paramref name="ownerId"/>'s key, so values can be
    /// encrypted for them in a <see cref="BeginWriting"/> scope.
    /// </summary>
    public async Task EnsureKeyAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        if (_keyIdByOwner.ContainsKey(ownerId))
            return;

        var stored = await _store.FindByOwnerAsync(ownerId, cancellationToken);
        if (stored is null)
        {
            var id = Guid.NewGuid();
            var key = RandomNumberGenerator.GetBytes(MasterKeyRing.KeySize);
            var created = new UserKey
            {
                Id = id,
                OwnerId = ownerId,
                MasterKeyId = _masterKeys.CurrentId,
                WrappedKey = Wrap(_masterKeys.Current, id, ownerId, key),
                CreatedAt = DateTime.UtcNow
            };
            stored = await _store.TryAddAsync(created, cancellationToken)
                ? created
                : await _store.FindByOwnerAsync(ownerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Could not create or load the data key of user {ownerId}.");
        }

        _keysById.TryAdd(stored.Id, Unwrap(stored));
        _keyIdByOwner[ownerId] = stored.Id;
    }

    /// <summary>Values encrypted until the scope is disposed use <paramref name="ownerId"/>'s key.</summary>
    public static IDisposable BeginWriting(string ownerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        var previous = WritingOwner.Value;
        WritingOwner.Value = ownerId;
        return new Scope(() => WritingOwner.Value = previous);
    }

    /// <summary>Counts the values read in plain text (not encrypted yet) until disposed.</summary>
    public static LegacyReadCounter CountLegacyReads()
    {
        var previous = LegacyReads.Value;
        var counter = new StrongBox<int>();
        LegacyReads.Value = counter;
        return new LegacyReadCounter(counter, () => LegacyReads.Value = previous);
    }

    /// <summary>Re-wraps the user keys that an older master key wrapped. Returns how many it re-wrapped.</summary>
    public async Task<int> RewrapUserKeysAsync(CancellationToken cancellationToken = default)
    {
        var rewrapped = 0;
        foreach (var stored in await _store.GetAllAsync(cancellationToken))
        {
            if (stored.MasterKeyId == _masterKeys.CurrentId)
                continue;
            var key = Unwrap(stored);
            await _store.UpdateWrappingAsync(stored.Id, _masterKeys.CurrentId,
                Wrap(_masterKeys.Current, stored.Id, stored.OwnerId, key), cancellationToken);
            rewrapped++;
        }
        return rewrapped;
    }

    public string EncryptText(string purpose, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return TextPrefix + Convert.ToBase64String(Encrypt(purpose, Encoding.UTF8.GetBytes(plaintext)));
    }

    public string DecryptText(string purpose, string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (!stored.StartsWith(TextPrefix, StringComparison.Ordinal))
        {
            NoteLegacyRead();
            return stored;
        }

        var payload = Convert.FromBase64String(stored[TextPrefix.Length..]);
        return Encoding.UTF8.GetString(Decrypt(purpose, payload));
    }

    public byte[] EncryptBytes(string purpose, byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return [.. BinaryPrefix, .. Encrypt(purpose, plaintext)];
    }

    public byte[] DecryptBytes(string purpose, byte[] stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (!stored.AsSpan().StartsWith(BinaryPrefix))
        {
            NoteLegacyRead();
            return stored;
        }

        return Decrypt(purpose, stored[BinaryPrefix.Length..]);
    }

    private byte[] Encrypt(string purpose, byte[] plaintext)
    {
        var owner = WritingOwner.Value
            ?? throw new InvalidOperationException(
                "An encrypted column can only be written by ApplicationDbContext.SaveChangesAsync, and can't be " +
                "compared in a query (compare it in memory instead).");
        if (!_keyIdByOwner.TryGetValue(owner, out var keyId) || !_keysById.TryGetValue(keyId, out var key))
            throw new InvalidOperationException($"The data key of user {owner} is not loaded (call EnsureKeyAsync first).");

        var output = new byte[HeaderSize + plaintext.Length];
        var span = output.AsSpan();
        keyId.TryWriteBytes(span[..KeyIdSize]);
        var nonce = span.Slice(KeyIdSize, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, span[HeaderSize..], span.Slice(KeyIdSize + NonceSize, TagSize),
            AssociatedData(purpose, keyId));
        return output;
    }

    private byte[] Decrypt(string purpose, byte[] payload)
    {
        if (payload.Length < HeaderSize)
            throw new CryptographicException("Encrypted value is truncated.");

        var span = payload.AsSpan();
        var keyId = new Guid(span[..KeyIdSize]);
        var plaintext = new byte[payload.Length - HeaderSize];
        using var aes = new AesGcm(KeyById(keyId), TagSize);
        aes.Decrypt(span.Slice(KeyIdSize, NonceSize), span[HeaderSize..], span.Slice(KeyIdSize + NonceSize, TagSize),
            plaintext, AssociatedData(purpose, keyId));
        return plaintext;
    }

    private byte[] KeyById(Guid keyId)
    {
        if (_keysById.TryGetValue(keyId, out var key))
            return key;

        var stored = _store.FindById(keyId)
            ?? throw new CryptographicException($"Data key {keyId} does not exist (deleted, or from another server).");
        key = Unwrap(stored);
        _keysById.TryAdd(keyId, key);
        return key;
    }

    private byte[] Unwrap(UserKey stored)
    {
        if (!_masterKeys.TryGet(stored.MasterKeyId, out var master))
            throw new CryptographicException(
                $"Data key {stored.Id} was wrapped by master key {stored.MasterKeyId}, which is not configured " +
                "(add its file to Encryption:PreviousKeyFiles).");

        var wrapped = stored.WrappedKey.AsSpan();
        if (wrapped.Length != NonceSize + TagSize + MasterKeyRing.KeySize)
            throw new CryptographicException($"Data key {stored.Id} is malformed.");

        var key = new byte[MasterKeyRing.KeySize];
        using var aes = new AesGcm(master, TagSize);
        aes.Decrypt(wrapped[..NonceSize], wrapped[(NonceSize + TagSize)..], wrapped.Slice(NonceSize, TagSize), key,
            WrapAssociatedData(stored.Id, stored.OwnerId));
        return key;
    }

    private static byte[] Wrap(byte[] master, Guid id, string ownerId, byte[] key)
    {
        var output = new byte[NonceSize + TagSize + key.Length];
        var span = output.AsSpan();
        RandomNumberGenerator.Fill(span[..NonceSize]);
        using var aes = new AesGcm(master, TagSize);
        aes.Encrypt(span[..NonceSize], key, span[(NonceSize + TagSize)..], span.Slice(NonceSize, TagSize),
            WrapAssociatedData(id, ownerId));
        return output;
    }

    private static byte[] AssociatedData(string purpose, Guid keyId) =>
        [.. Encoding.UTF8.GetBytes(purpose), 0, .. keyId.ToByteArray()];

    private static byte[] WrapAssociatedData(Guid id, string ownerId) =>
        [.. WrapPurpose, 0, .. id.ToByteArray(), 0, .. Encoding.UTF8.GetBytes(ownerId)];

    private static void NoteLegacyRead()
    {
        if (LegacyReads.Value is { } counter)
            Interlocked.Increment(ref counter.Value);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    public sealed class LegacyReadCounter(StrongBox<int> counter, Action dispose) : IDisposable
    {
        public int Count => Volatile.Read(ref counter.Value);

        public void Dispose() => dispose();
    }
}
