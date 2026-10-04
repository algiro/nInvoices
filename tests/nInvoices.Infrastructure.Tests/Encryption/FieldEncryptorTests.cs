using System.Security.Cryptography;
using nInvoices.Infrastructure.Encryption;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Encryption;

[TestFixture]
public sealed class FieldEncryptorTests
{
    private const string Alice = "alice-sub";
    private const string Bob = "bob-sub";

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static byte[] NewMasterKey() => RandomNumberGenerator.GetBytes(MasterKeyRing.KeySize);

    private static async Task<string> EncryptAsync(FieldEncryptor encryptor, string owner, string purpose, string value)
    {
        await encryptor.EnsureKeyAsync(owner, Token);
        using (FieldEncryptor.BeginWriting(owner))
            return encryptor.EncryptText(purpose, value);
    }

    [Test]
    public async Task EncryptText_ThenDecrypt_ReturnsOriginal()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();

        var stored = await EncryptAsync(encryptor, Alice, "Customer.Name", "Acme S.p.A. — Città");

        stored.ShouldStartWith(FieldEncryptor.TextPrefix);
        stored.ShouldNotContain("Acme");
        encryptor.DecryptText("Customer.Name", stored).ShouldBe("Acme S.p.A. — Città");
    }

    [Test]
    public async Task EncryptText_SameValueTwice_GivesDifferentCiphertexts()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();

        var first = await EncryptAsync(encryptor, Alice, "Customer.Name", "Acme");
        var second = await EncryptAsync(encryptor, Alice, "Customer.Name", "Acme");

        first.ShouldNotBe(second);
    }

    [Test]
    public async Task EncryptBytes_ThenDecrypt_ReturnsOriginal()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();
        await encryptor.EnsureKeyAsync(Alice, Token);
        byte[] content = [1, 2, 3, 0, 255];

        byte[] stored;
        using (FieldEncryptor.BeginWriting(Alice))
            stored = encryptor.EncryptBytes("InvoiceEInvoice.Content", content);

        stored.ShouldNotBe(content);
        encryptor.DecryptBytes("InvoiceEInvoice.Content", stored).ShouldBe(content);
    }

    [Test]
    public void DecryptText_PlainLegacyValue_ReturnsItAndCountsIt()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();

        using var legacy = FieldEncryptor.CountLegacyReads();
        encryptor.DecryptText("Customer.Name", "Acme").ShouldBe("Acme");

        legacy.Count.ShouldBe(1);
    }

    [Test]
    public async Task DecryptText_OtherColumnsPurpose_Fails()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();
        var stored = await EncryptAsync(encryptor, Alice, "Customer.Name", "Acme");

        Should.Throw<CryptographicException>(() => encryptor.DecryptText("Customer.Email", stored));
    }

    [Test]
    public async Task DecryptText_TamperedValue_Fails()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();
        var stored = await EncryptAsync(encryptor, Alice, "Customer.Name", "Acme");
        var bytes = Convert.FromBase64String(stored[FieldEncryptor.TextPrefix.Length..]);
        bytes[^1] ^= 1;

        Should.Throw<CryptographicException>(() =>
            encryptor.DecryptText("Customer.Name", FieldEncryptor.TextPrefix + Convert.ToBase64String(bytes)));
    }

    [Test]
    public void EncryptText_OutsideAWritingScope_Throws()
    {
        var encryptor = FieldEncryptor.CreateEphemeral();

        Should.Throw<InvalidOperationException>(() => encryptor.EncryptText("Customer.Name", "Acme"));
    }

    [Test]
    public async Task EnsureKeyAsync_TwoUsers_GetDifferentKeys()
    {
        var store = new InMemoryUserKeyStore();
        var encryptor = new FieldEncryptor(MasterKeyRing.FromKeys(NewMasterKey()), store);

        await encryptor.EnsureKeyAsync(Alice, Token);
        await encryptor.EnsureKeyAsync(Bob, Token);
        await encryptor.EnsureKeyAsync(Alice, Token);

        var keys = await store.GetAllAsync(Token);
        keys.Count.ShouldBe(2);
        keys.Select(k => k.OwnerId).ShouldBe([Alice, Bob], ignoreOrder: true);
    }

    [Test]
    public async Task DecryptText_AfterRestartWithSameMasterKey_Works()
    {
        var master = NewMasterKey();
        var store = new InMemoryUserKeyStore();
        var stored = await EncryptAsync(new FieldEncryptor(MasterKeyRing.FromKeys(master), store), Alice, "Customer.Name", "Acme");

        // A new process: nothing cached, the user key is loaded from the store by the id in the value
        var restarted = new FieldEncryptor(MasterKeyRing.FromKeys(master), store);

        restarted.DecryptText("Customer.Name", stored).ShouldBe("Acme");
    }

    [Test]
    public async Task DecryptText_WithAnotherMasterKey_Fails()
    {
        var store = new InMemoryUserKeyStore();
        var stored = await EncryptAsync(new FieldEncryptor(MasterKeyRing.FromKeys(NewMasterKey()), store), Alice, "Customer.Name", "Acme");

        var wrongMaster = new FieldEncryptor(MasterKeyRing.FromKeys(NewMasterKey()), store);

        Should.Throw<CryptographicException>(() => wrongMaster.DecryptText("Customer.Name", stored))
            .Message.ShouldContain("not configured");
    }

    [Test]
    public async Task DecryptText_UserKeyDeleted_Fails()
    {
        var master = NewMasterKey();
        var stored = await EncryptAsync(new FieldEncryptor(MasterKeyRing.FromKeys(master), new InMemoryUserKeyStore()), Alice, "Customer.Name", "Acme");

        // The data outlives its key (crypto-shredding): a store without the key can't read it
        var withoutKey = new FieldEncryptor(MasterKeyRing.FromKeys(master), new InMemoryUserKeyStore());

        Should.Throw<CryptographicException>(() => withoutKey.DecryptText("Customer.Name", stored));
    }

    [Test]
    public async Task RewrapUserKeysAsync_AfterRotation_OldMasterKeyNoLongerNeeded()
    {
        var oldMaster = NewMasterKey();
        var newMaster = NewMasterKey();
        var store = new InMemoryUserKeyStore();
        var stored = await EncryptAsync(new FieldEncryptor(MasterKeyRing.FromKeys(oldMaster), store), Alice, "Customer.Name", "Acme");

        var rotated = await new FieldEncryptor(MasterKeyRing.FromKeys(newMaster, oldMaster), store).RewrapUserKeysAsync(Token);

        rotated.ShouldBe(1);
        new FieldEncryptor(MasterKeyRing.FromKeys(newMaster), store).DecryptText("Customer.Name", stored).ShouldBe("Acme");
    }

    [Test]
    public void Load_MissingKeyFile_ThrowsUnlessAskedToCreateIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ninvoices-test-{Guid.NewGuid():N}", "master.key");
        try
        {
            Should.Throw<InvalidOperationException>(() => MasterKeyRing.Load(path, [], createIfMissing: false))
                .Message.ShouldContain("not found");

            var created = MasterKeyRing.Load(path, [], createIfMissing: true);
            var reloaded = MasterKeyRing.Load(path, [], createIfMissing: false);

            reloaded.CurrentId.ShouldBe(created.CurrentId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Test]
    public void Load_MalformedKeyFile_Throws()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a key");

            Should.Throw<InvalidOperationException>(() => MasterKeyRing.Load(path, [], createIfMissing: false));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
