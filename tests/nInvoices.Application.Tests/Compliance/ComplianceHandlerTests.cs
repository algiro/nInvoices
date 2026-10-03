using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Compliance.Spain;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Features.Compliance.Queries;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Compliance;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class ComplianceHandlerTests
{
    private InMemoryRepository<ComplianceSettings> _settings = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static readonly AddressDto SpanishAddress = new("Calle Mayor", "1", "Madrid", "28013", "Spain");

    private static readonly Dictionary<string, string> Individual = new() { [SpainComplianceModule.PersonTypeKey] = "individual" };

    [SetUp]
    public void SetUp()
    {
        _settings = new InMemoryRepository<ComplianceSettings>();
        _unitOfWork = new Mock<IUnitOfWork>();
    }

    private static IComplianceRegistry Registry(ComplianceOptions? options = null) =>
        new ComplianceRegistry([new SpainComplianceModule()], Options.Create(options ?? new ComplianceOptions()));

    private Task<ComplianceCountryDto?> Update(string country, UpdateComplianceSettingsDto dto, IComplianceRegistry? registry = null) =>
        new UpdateComplianceSettingsCommandHandler(registry ?? Registry(), _settings, _unitOfWork.Object)
            .Handle(new UpdateComplianceSettingsCommand(country, dto), Token);

    private static UpdateComplianceSettingsDto ValidSpain(bool enabled = true) =>
        new(enabled, "Ana Pérez", "12345678Z", SpanishAddress, Individual);

    [Test]
    public async Task Get_NothingSaved_OffersSpainDisabled()
    {
        var countries = await new GetComplianceCountriesQueryHandler(Registry(), _settings)
            .Handle(new GetComplianceCountriesQuery(), Token);

        var spain = countries.ShouldHaveSingleItem();
        spain.CountryCode.ShouldBe("ES");
        spain.Settings.IsEnabled.ShouldBeFalse();
        spain.Fields.ShouldContain(f => f.Key == SpainComplianceModule.PersonTypeKey);
    }

    [Test]
    public async Task Get_CountrySwitchedOffByTheInstallation_IsNotOffered()
    {
        var options = new ComplianceOptions { Countries = { ["ES"] = new CountryComplianceOptions { Enabled = false } } };

        var countries = await new GetComplianceCountriesQueryHandler(Registry(options), _settings)
            .Handle(new GetComplianceCountriesQuery(), Token);

        countries.ShouldBeEmpty();
    }

    [Test]
    public async Task Update_Valid_SavesAndEnables()
    {
        var result = await Update("es", ValidSpain());

        result.ShouldNotBeNull();
        result.Settings.IsEnabled.ShouldBeTrue();
        var saved = _settings.Items.ShouldHaveSingleItem();
        saved.CountryCode.ShouldBe("ES");
        saved.TaxId.ShouldBe("12345678Z");
        saved.Address!.City.ShouldBe("Madrid");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Update_ExistingRecord_UpdatesItInsteadOfAddingAnother()
    {
        await Update("ES", ValidSpain());
        await Update("ES", ValidSpain() with { LegalName = "Ana Pérez SL" });

        _settings.Items.ShouldHaveSingleItem().LegalName.ShouldBe("Ana Pérez SL");
    }

    [Test]
    public async Task Update_EnablingWithBrokenIssuer_ReportsEveryIssueAndSavesNothing()
    {
        var dto = new UpdateComplianceSettingsDto(true, null, "12345678A", null, null);

        var ex = await Should.ThrowAsync<ComplianceValidationException>(() => Update("ES", dto));

        ex.Issues.Select(i => i.Field).ShouldBe(["legalName", "taxId", "address", SpainComplianceModule.PersonTypeKey], ignoreOrder: true);
        _settings.Items.ShouldBeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_WhileOff_SavesIncompleteSettingsWithoutValidating()
    {
        var result = await Update("ES", new UpdateComplianceSettingsDto(false, "Ana", "not-a-nif", null, null));

        result.ShouldNotBeNull();
        result.Settings.IsEnabled.ShouldBeFalse();
        _settings.Items.ShouldHaveSingleItem().TaxId.ShouldBe("not-a-nif");
    }

    [Test]
    public async Task Update_UnknownValueKeys_AreDropped()
    {
        var dto = ValidSpain() with { Values = new Dictionary<string, string>(Individual) { ["bogus"] = "x" } };

        await Update("ES", dto);

        _settings.Items.Single().Values.Keys.ShouldBe([SpainComplianceModule.PersonTypeKey]);
    }

    [Test]
    public async Task Update_CountryNotOffered_ReturnsNull()
    {
        (await Update("IT", ValidSpain())).ShouldBeNull();
        _settings.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Gate_OnlyEnabledOfferedCountriesApply()
    {
        var gate = new ComplianceGate(Registry(), _settings);
        (await gate.IsEnabledAsync("ES", Token)).ShouldBeFalse();

        await Update("ES", ValidSpain(enabled: false));
        (await gate.IsEnabledAsync("ES", Token)).ShouldBeFalse();

        await Update("ES", ValidSpain());
        (await gate.IsEnabledAsync("ES", Token)).ShouldBeTrue();
        (await gate.IsEnabledAsync("IT", Token)).ShouldBeFalse();
    }

    [Test]
    public async Task Gate_EnabledByTheUserButSwitchedOffByTheInstallation_DoesNotApply()
    {
        await Update("ES", ValidSpain());
        var off = new ComplianceOptions { Countries = { ["ES"] = new CountryComplianceOptions { Enabled = false } } };

        (await new ComplianceGate(Registry(off), _settings).IsEnabledAsync("ES", Token)).ShouldBeFalse();
    }
}
