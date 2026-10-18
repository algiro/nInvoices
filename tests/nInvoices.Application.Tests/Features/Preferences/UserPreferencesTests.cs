using Moq;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Preferences;
using nInvoices.Application.Localization;
using nInvoices.Application.Tests.TestDoubles;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Preferences;

/// <summary>The app's language is the user's, on the server: English until they pick another.</summary>
[TestFixture]
public sealed class UserPreferencesTests
{
    private static readonly AppText Text = new(new Dictionary<string, string>
    {
        ["en"] = """{ "preferences": { "chooseLanguage": "Choose a language", "unsupportedLanguage": "{language} is not one of {languages}" } }""",
        ["it"] = "{}"
    });

    private InMemoryRepository<UserPreferences> _preferences = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;

    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        _preferences = new InMemoryRepository<UserPreferences>();
        _unitOfWork = new Mock<IUnitOfWork>();
    }

    private Task<UserPreferencesDto> GetAsync() =>
        new GetUserPreferencesQueryHandler(_preferences, Text).Handle(new GetUserPreferencesQuery(), Token).AsTask();

    private Task<UserPreferencesDto> UpdateAsync(string language) =>
        new UpdateUserPreferencesCommandHandler(_preferences, _unitOfWork.Object, Text)
            .Handle(new UpdateUserPreferencesCommand(new UpdateUserPreferencesDto(language)), Token).AsTask();

    [Test]
    public async Task Get_NewUser_IsEnglish()
    {
        var preferences = await GetAsync();

        preferences.UiLanguage.ShouldBe("en");
        preferences.UiLanguages.ShouldBe(["en", "it"]);
    }

    [Test]
    public async Task Update_NewUser_StoresTheLanguage()
    {
        var preferences = await UpdateAsync("it");

        preferences.UiLanguage.ShouldBe("it");
        (await GetAsync()).UiLanguage.ShouldBe("it");
        (await _preferences.GetAllAsync(Token)).Single().UiLanguage.ShouldBe("it");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Update_AgainChangesTheSameRow()
    {
        await UpdateAsync("it");

        await UpdateAsync("en");

        (await _preferences.GetAllAsync(Token)).Single().UiLanguage.ShouldBe("en");
    }

    [TestCase("IT")]
    [TestCase("it-CH")]
    public async Task Update_StoresTheAppsLanguage(string language)
    {
        (await UpdateAsync(language)).UiLanguage.ShouldBe("it");
    }

    [Test]
    public async Task Get_LanguageNoLongerShipped_IsEnglish()
    {
        // Chosen when the app had French; its files were removed since
        await _preferences.AddAsync(new UserPreferences("fr"), Token);

        (await GetAsync()).UiLanguage.ShouldBe("en");
    }

    [TestCase("it", true)]
    [TestCase("it-IT", true)]
    [TestCase("fr", false)]
    [TestCase("", false)]
    public async Task Validator_AcceptsOnlyTheAppsLanguages(string language, bool valid)
    {
        var result = await new UpdateUserPreferencesCommandValidator(Text)
            .ValidateAsync(new UpdateUserPreferencesCommand(new UpdateUserPreferencesDto(language)), Token);

        result.IsValid.ShouldBe(valid);
        if (language == "fr")
            result.Errors.Single().ErrorMessage.ShouldBe("fr is not one of en, it");
    }
}
