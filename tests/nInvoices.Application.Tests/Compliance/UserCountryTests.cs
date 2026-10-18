using Microsoft.Extensions.Options;
using Moq;
using nInvoices.Application.Compliance;
using nInvoices.Application.Countries;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Features.Country;
using nInvoices.Application.Features.TaxPrediction.Commands;
using nInvoices.Application.TaxPrediction;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;
using nInvoices.Countries.Italy;
using nInvoices.Countries.Italy.TaxPrediction;
using nInvoices.Countries.Spain;
using nInvoices.Countries.Spain.TaxPrediction;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

/// <summary>A freelancer works from one country: only its invoicing rules and tax regime apply.</summary>
[TestFixture]
public sealed class UserCountryTests
{
    private InMemoryRepository<UserCountry> _countries = null!;
    private InMemoryRepository<ComplianceSettings> _compliance = null!;
    private InMemoryRepository<TaxProfile> _profiles = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private UserCountryService _country = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private static readonly IComplianceRegistry ComplianceRegistry = new ComplianceRegistry(
        [new SpainComplianceModule(TestText.Instance), new ItalyComplianceModule(TestText.Instance)], [], Options.Create(new ComplianceOptions()));

    private static readonly ITaxPredictionRegistry TaxRegistry = new TaxPredictionRegistry(
        [new SpainTaxPredictionModule(), new ItalyTaxPredictionModule()]);

    [SetUp]
    public void SetUp()
    {
        _countries = new InMemoryRepository<UserCountry>();
        _compliance = new InMemoryRepository<ComplianceSettings>();
        _profiles = new InMemoryRepository<TaxProfile>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _country = new UserCountryService(_countries, _compliance, _profiles);
    }

    private async Task<ComplianceSettings> SeedComplianceAsync(string country, bool enabled)
    {
        var settings = new ComplianceSettings(country);
        settings.Update(enabled, "Name", "X", null, new Dictionary<string, string> { ["kept"] = "yes" });
        await _compliance.AddAsync(settings, Token);
        return settings;
    }

    private Task<UserCountryDto> SetAsync(string country) =>
        new SetUserCountryCommandHandler(_countries, _compliance, ComplianceRegistry, TaxRegistry, _unitOfWork.Object, TestText.Instance)
            .Handle(new SetUserCountryCommand(new SetUserCountryDto(country)), Token).AsTask();

    [Test]
    public async Task Get_NothingUsedYet_IsUnknown()
    {
        (await _country.GetAsync(Token)).ShouldBeNull();
        (await _country.IsChosenAsync(Token)).ShouldBeFalse();
    }

    [Test]
    public async Task Get_NotChosen_IsTheCountryWhoseRulesAreOn()
    {
        await SeedComplianceAsync("IT", enabled: false);
        await SeedComplianceAsync("ES", enabled: true);

        (await _country.GetAsync(Token)).ShouldBe("ES");
    }

    [Test]
    public async Task Get_NotChosen_PrefersTheTaxProfileAmongSeveralTurnedOn()
    {
        await SeedComplianceAsync("ES", enabled: true);
        await SeedComplianceAsync("IT", enabled: true);
        await _profiles.AddAsync(new TaxProfile("IT", ItalyTaxPredictionModule.ForfettarioRegime, null), Token);

        (await _country.GetAsync(Token)).ShouldBe("IT");
    }

    [Test]
    public async Task Get_NotChosen_WithOnlyATaxProfile_IsItsCountry()
    {
        await _profiles.AddAsync(new TaxProfile("IT", ItalyTaxPredictionModule.ForfettarioRegime, null), Token);

        (await _country.GetAsync(Token)).ShouldBe("IT");
    }

    [Test]
    public async Task Set_StoresIt_AndTurnsTheOtherCountriesOff_KeepingTheirSettings()
    {
        var spain = await SeedComplianceAsync("ES", enabled: true);

        var result = await SetAsync("it");

        result.CountryCode.ShouldBe("IT");
        result.IsChosen.ShouldBeTrue();
        (await _country.GetAsync(Token)).ShouldBe("IT");
        spain.IsEnabled.ShouldBeFalse();
        spain.Values["kept"].ShouldBe("yes");
        result.Offered.Select(c => (c.CountryCode, c.HasInvoicingRules, c.HasTaxPrediction))
            .ShouldBe([("IT", true, true), ("ES", true, true)]);
    }

    [Test]
    public async Task Set_ACountryNotSupported_IsRefused() =>
        await Should.ThrowAsync<DomainException>(SetAsync("FR"));

    [Test]
    public async Task Gate_SeveralCountriesTurnedOn_AppliesOnlyTheUsersCountry()
    {
        await SeedComplianceAsync("ES", enabled: true);
        await SeedComplianceAsync("IT", enabled: true);
        await _countries.AddAsync(new UserCountry("IT"), Token);

        var active = await new ComplianceGate(ComplianceRegistry, _compliance, _country).GetActiveAsync(Token);

        active.ShouldHaveSingleItem().Module.CountryCode.ShouldBe("IT");
    }

    [Test]
    public async Task TurningOnAnotherCountrysRules_IsRefused()
    {
        await _countries.AddAsync(new UserCountry("IT"), Token);
        var handler = new UpdateComplianceSettingsCommandHandler(
            ComplianceRegistry, _compliance, _unitOfWork.Object, new FixedTimeProvider(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)),
            TestText.Instance, _country, _countries);

        var error = await Should.ThrowAsync<DomainException>(handler.Handle(
            new UpdateComplianceSettingsCommand("ES", new UpdateComplianceSettingsDto(true, "Ana", "12345678Z", null, null)), Token).AsTask());

        error.Message.ShouldContain("Country & taxes");
    }

    [Test]
    public async Task TurningOnRules_WithoutACountry_MakesItTheUsersCountry()
    {
        var handler = new UpdateComplianceSettingsCommandHandler(
            ComplianceRegistry, _compliance, _unitOfWork.Object, new FixedTimeProvider(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)),
            TestText.Instance, _country, _countries);
        var dto = new UpdateComplianceSettingsDto(
            true,
            "Ana Pérez",
            "12345678Z",
            new AddressDto("Calle Mayor", "1", "Madrid", "28013", "Spain", "Madrid"),
            new Dictionary<string, string>
            {
                [SpainComplianceModule.PersonTypeKey] = SpainComplianceModule.Individual,
                [SpainComplianceModule.FirstNameKey] = "Ana",
                [SpainComplianceModule.FirstSurnameKey] = "Pérez"
            });

        await handler.Handle(new UpdateComplianceSettingsCommand("ES", dto), Token);

        (await _countries.GetAllAsync(Token)).ShouldHaveSingleItem().CountryCode.ShouldBe("ES");
    }

    [Test]
    public async Task ATaxProfileOfAnotherCountry_IsRefused()
    {
        await _countries.AddAsync(new UserCountry("IT"), Token);
        var handler = new UpdateTaxProfileCommandHandler(TaxRegistry, _profiles, _unitOfWork.Object, TestText.Instance, _country, _countries);

        await Should.ThrowAsync<DomainException>(handler.Handle(
            new UpdateTaxProfileCommand(new UpdateTaxProfileDto("ES", SpainTaxPredictionModule.SimplifiedDirectEstimation,
                new Dictionary<string, string> { [SpainTaxPredictionModule.TerritoryKey] = SpainTaxPredictionModule.Mainland })), Token).AsTask());
    }
}
