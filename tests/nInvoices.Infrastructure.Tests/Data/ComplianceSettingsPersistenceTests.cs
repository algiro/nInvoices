using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using nInvoices.Infrastructure.Data;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.Data;

[TestFixture]
public sealed class ComplianceSettingsPersistenceTests
{
    private SqliteConnection _connection = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(Token);
        await ContextFor(null).Database.EnsureCreatedAsync(Token);
    }

    [TearDown]
    public async Task TearDown() => await _connection.DisposeAsync();

    private ApplicationDbContext ContextFor(string? userId) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options, TestEncryption.Encryptor, new TestUserContext(userId));

    private static ComplianceSettings Spain()
    {
        var settings = new ComplianceSettings("es");
        settings.Update(true, "Ana", "12345678Z", new Address("Calle Mayor", "1", "Madrid", "28013", "Spain"),
            new Dictionary<string, string> { ["personType"] = "individual" });
        return settings;
    }

    [Test]
    public async Task RoundTrip_KeepsIdentityAddressAndValues()
    {
        var context = ContextFor("alice");
        context.ComplianceSettings.Add(Spain());
        await context.SaveChangesAsync(Token);

        var loaded = await ContextFor("alice").ComplianceSettings.SingleAsync(Token);

        loaded.CountryCode.ShouldBe("ES");
        loaded.IsEnabled.ShouldBeTrue();
        loaded.Address!.ZipCode.ShouldBe("28013");
        loaded.Values.ShouldBe(new Dictionary<string, string> { ["personType"] = "individual" });
    }

    [Test]
    public async Task ChangingValues_IsPersisted()
    {
        var context = ContextFor("alice");
        context.ComplianceSettings.Add(Spain());
        await context.SaveChangesAsync(Token);

        var edit = ContextFor("alice");
        var settings = await edit.ComplianceSettings.SingleAsync(Token);
        settings.Update(true, "Ana", "12345678Z", settings.Address, new Dictionary<string, string> { ["personType"] = "legalEntity" });
        await edit.SaveChangesAsync(Token);

        (await ContextFor("alice").ComplianceSettings.SingleAsync(Token)).Values["personType"].ShouldBe("legalEntity");
    }

    [Test]
    public async Task OtherUsers_DoNotSeeIt_AndMayHaveTheirOwnForTheSameCountry()
    {
        var alice = ContextFor("alice");
        alice.ComplianceSettings.Add(Spain());
        await alice.SaveChangesAsync(Token);

        (await ContextFor("bob").ComplianceSettings.CountAsync(Token)).ShouldBe(0);

        var bob = ContextFor("bob");
        bob.ComplianceSettings.Add(Spain());
        await Should.NotThrowAsync(() => bob.SaveChangesAsync(Token));
    }

    [Test]
    public async Task SameUserAndCountryTwice_IsRejected()
    {
        var context = ContextFor("alice");
        context.ComplianceSettings.Add(Spain());
        context.ComplianceSettings.Add(Spain());

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }
}
