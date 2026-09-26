using Microsoft.Extensions.Logging;
using Moq;
using nInvoices.Application.Models;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

/// <summary>
/// How the variables of an <see cref="InvoiceTemplateModel"/> and the functions registered by
/// <see cref="ScribanTemplateRenderer"/> resolve in a template. The variable names are the ones the
/// template editor offers (templateVariables.ts), so a rename on either side breaks these tests.
/// </summary>
[TestFixture]
[SetCulture("en-US")]
public sealed class InvoiceTemplateVariablesTests
{
    private Mock<ILocalizationService> _localization = null!;
    private List<ImageAsset> _images = null!;
    private ScribanTemplateRenderer _renderer = null!;

    [SetUp]
    public void SetUp()
    {
        _localization = new Mock<ILocalizationService>();
        _images = [];
        var imageRepository = new Mock<IRepository<ImageAsset>>();
        imageRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _images);
        _renderer = new ScribanTemplateRenderer(Mock.Of<ILogger<ScribanTemplateRenderer>>(), _localization.Object, imageRepository.Object);
    }

    private static InvoiceTemplateModel MonthlyInvoice() => new()
    {
        InvoiceNumber = "26-09-004",
        InvoiceType = "Monthly",
        Date = new DateTime(2026, 9, 30),
        DueDate = new DateTime(2026, 10, 31),
        Currency = "EUR",
        Locale = "it-IT",
        Customer = new CustomerTemplateModel
        {
            Name = "Northwind Capital S.p.A.",
            FiscalId = "IT01234567890",
            Address = new AddressTemplateModel { Street = "Via Roma 10", PostalCode = "20121", City = "Milano", Country = "Italy" }
        },
        LineItems =
        [
            new LineItemTemplateModel { Description = "Trade reporting gateway", Quantity = 18.5m, Rate = 500m, Amount = 9250m },
            new LineItemTemplateModel { Description = "FIX certification", Quantity = 2m, Rate = 500m, Amount = 1000m }
        ],
        Taxes =
        [
            new TaxTemplateModel { Description = "VAT", Rate = 22m, Amount = 2255m },
            new TaxTemplateModel { Description = "Withholding", Rate = -20m, Amount = -2050m }
        ],
        ProjectSummary =
        [
            new ProjectSummaryTemplateModel { Name = "FIX certification", WorkedDays = 2, TotalHours = 16m, Amount = 1000m },
            new ProjectSummaryTemplateModel { Name = "Trade reporting gateway", WorkedDays = 19, TotalHours = 148m, Amount = 9250m }
        ],
        Subtotal = 10250m,
        TotalTax = 205m,
        Total = 10455m,
        WorkedDays = 21,
        MonthNumber = 9,
        MonthDescription = "September 2026",
        MonthlyRate = 500m,
        TotalExpenses = 120.5m,
        WorkedDayItems =
        [
            new WorkedDayTemplateModel { Date = "01/09/2026", Hours = 8m },
            new WorkedDayTemplateModel { Date = "02/09/2026", Hours = 4m }
        ]
    };

    private static InvoiceTemplateModel OneTimeInvoice() => MonthlyInvoice() with
    {
        InvoiceType = "OneTime",
        DueDate = null,
        WorkedDays = null,
        MonthNumber = null,
        MonthDescription = null,
        MonthlyRate = null,
        TotalExpenses = null,
        WorkedDayItems = [],
        ProjectSummary = []
    };

    private Task<string> Render(string template, object? model = null) =>
        _renderer.RenderAsync(template, model ?? MonthlyInvoice(), TestContext.CurrentContext.CancellationToken);

    /// <summary>Renders the invoice (or <paramref name="model"/>'s members) plus a <c>loc</c> variable holding <paramref name="locale"/>.</summary>
    private Task<string> Render(string template, InvoiceTemplateModel? model, string locale) =>
        Render(template, new LocalizedInvoice(model ?? MonthlyInvoice(), locale));

    /// <summary>The invoice members a test uses, with a locale alongside as a template variable would provide it.</summary>
    private sealed record LocalizedInvoice(InvoiceTemplateModel Invoice, string Loc)
    {
        public decimal Total => Invoice.Total;
        public string Currency => Invoice.Currency;
        public DateTime Date => Invoice.Date;
    }

    // ---------- plain variables ----------

    [TestCase("[[ invoiceNumber ]]", "26-09-004")]
    [TestCase("[[ invoiceType ]]", "Monthly")]
    [TestCase("[[ currency ]]", "EUR")]
    [TestCase("[[ locale ]]", "it-IT")]
    [TestCase("[[ subtotal ]]", "10250")]
    [TestCase("[[ totalTax ]]", "205")]
    [TestCase("[[ total ]]", "10455")]
    [TestCase("[[ totalExpenses ]]", "120.5")]
    [TestCase("[[ monthDescription ]]", "September 2026")]
    [TestCase("[[ monthNumber ]]", "9")]
    [TestCase("[[ workedDays ]]", "21")]
    [TestCase("[[ monthlyRate ]]", "500")]
    public async Task InvoiceVariable_ResolvesToModelValue(string template, string expected)
    {
        (await Render(template)).ShouldBe(expected);
    }

    [TestCase("[[ customer.name ]]", "Northwind Capital S.p.A.")]
    [TestCase("[[ customer.fiscalId ]]", "IT01234567890")]
    [TestCase("[[ customer.address.street ]]", "Via Roma 10")]
    [TestCase("[[ customer.address.postalCode ]]", "20121")]
    [TestCase("[[ customer.address.city ]]", "Milano")]
    [TestCase("[[ customer.address.country ]]", "Italy")]
    public async Task CustomerVariable_ResolvesNestedCamelCaseMembers(string template, string expected)
    {
        (await Render(template)).ShouldBe(expected);
    }

    [Test]
    public async Task Variable_InPascalCase_DoesNotResolve()
    {
        // Templates use camelCase only; the PascalCase C# name renders as an unknown (empty) variable
        (await Render("<[[ InvoiceNumber ]]|[[ customer.Name ]]>")).ShouldBe("<|>");
    }

    [Test]
    public async Task UnknownVariable_RendersEmpty()
    {
        (await Render("<p>[[ notAVariable ]]</p>")).ShouldBe("<p></p>");
    }

    [Test]
    public async Task CurlyBraceSyntax_IsAcceptedAsWell()
    {
        // The stored default template still uses Scriban's own {{ }} delimiters
        (await Render("{{ invoiceNumber }} / [[ currency ]]")).ShouldBe("26-09-004 / EUR");
    }

    [Test]
    public async Task MonthlyVariables_OnOneTimeInvoice_RenderEmpty()
    {
        var result = await Render("[[ monthDescription ]]|[[ workedDays ]]|[[ monthNumber ]]|[[ monthlyRate ]]|[[ totalExpenses ]]|[[ dueDate ]]", OneTimeInvoice());

        result.ShouldBe("|||||");
    }

    // ---------- loops & conditions ----------

    [Test]
    public async Task LineItemsLoop_ExposesEveryLineItemMember()
    {
        var result = await Render("[[ for item in lineItems ]][[ item.description ]];[[ item.quantity ]];[[ item.rate ]];[[ FormatCurrency item.amount currency ]]\n[[ end ]]");

        result.ShouldBe("Trade reporting gateway;18.5;500;9.250,00 EUR\nFIX certification;2;500;1.000,00 EUR\n");
    }

    [Test]
    public async Task TaxesLoop_ExposesEveryTaxMember()
    {
        var result = await Render("[[ for tax in taxes ]][[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%): [[ FormatCurrency tax.amount currency ]]\n[[ end ]]");

        result.ShouldBe("VAT (22.00%): 2.255,00 EUR\nWithholding (-20.00%): -2.050,00 EUR\n");
    }

    [Test]
    public async Task WorkedDayItemsLoop_ExposesDateAndHours()
    {
        var result = await Render("[[ for day in workedDayItems ]][[ day.date ]] [[ day.hours ]]h,[[ end ]]");

        result.ShouldBe("01/09/2026 8h,02/09/2026 4h,");
    }

    [Test]
    public async Task ProjectSummaryLoop_ExposesEveryProjectMember()
    {
        var result = await Render("[[ for p in projectSummary ]][[ p.name ]]: [[ p.workedDays ]] days, [[ p.totalHours ]]h, [[ FormatCurrency p.amount currency ]]\n[[ end ]]");

        result.ShouldBe("FIX certification: 2 days, 16h, 1.000,00 EUR\nTrade reporting gateway: 19 days, 148h, 9.250,00 EUR\n");
    }

    [Test]
    public async Task Loops_OverEmptyCollections_RenderNothing()
    {
        var result = await Render("[[ for d in workedDayItems ]]x[[ end ]][[ for p in projectSummary ]]y[[ end ]]", OneTimeInvoice());

        result.ShouldBeEmpty();
    }

    [Test]
    public async Task LoopHelpers_ForIndexAndFirstLast_AreAvailable()
    {
        var result = await Render("[[ for item in lineItems ]][[ for.index ]][[ if for.first ]]F[[ end ]][[ if for.last ]]L[[ end ]] [[ end ]]");

        result.ShouldBe("0F 1L ");
    }

    [Test]
    public async Task CollectionSize_IsAvailableThroughArraySize()
    {
        (await Render("[[ lineItems | array.size ]] items, [[ taxes.size ]] taxes")).ShouldBe("2 items, 2 taxes");
    }

    [Test]
    public async Task IfWorkedDays_IsTrueForMonthlyInvoice_AndFalseForOneTime()
    {
        const string template = "[[ if workedDays ]]monthly[[ else ]]one-time[[ end ]]";

        (await Render(template)).ShouldBe("monthly");
        (await Render(template, OneTimeInvoice())).ShouldBe("one-time");
    }

    [Test]
    public async Task IfTotalExpenses_IsFalseWhenNull()
    {
        const string template = "[[ if totalExpenses ]]expenses[[ else ]]none[[ end ]]";

        (await Render(template)).ShouldBe("expenses");
        (await Render(template, OneTimeInvoice())).ShouldBe("none");
    }

    [Test]
    public async Task IfDueDate_IsFalseWhenNull()
    {
        const string template = "[[ if dueDate ]]Due [[ FormatDate dueDate 'dd/MM/yyyy' ]][[ else ]]On receipt[[ end ]]";

        (await Render(template)).ShouldBe("Due 31/10/2026");
        (await Render(template, OneTimeInvoice())).ShouldBe("On receipt");
    }

    [Test]
    public async Task InvoiceType_CanBeComparedAsString()
    {
        (await Render("[[ if invoiceType == 'Monthly' ]]M[[ else ]]O[[ end ]]", OneTimeInvoice())).ShouldBe("O");
    }

    // ---------- FormatCurrency ----------

    [TestCase(1234.567, "1.234,57 EUR")]
    [TestCase(1234.565, "1.234,57 EUR")] // decimal rounding is away from zero, not banker's
    [TestCase(0, "0,00 EUR")]
    [TestCase(0.5, "0,50 EUR")]
    [TestCase(999.999, "1.000,00 EUR")]
    [TestCase(1234567.8, "1.234.567,80 EUR")]
    [TestCase(-2050, "-2.050,00 EUR")]
    public async Task FormatCurrency_UsesItalianGroupingAndTwoDecimals(decimal amount, string expected)
    {
        (await Render("[[ FormatCurrency amount 'EUR' ]]", new { Amount = amount })).ShouldBe(expected);
    }

    [TestCase("USD", "10.250,00 USD")]
    [TestCase("CHF", "10.250,00 CHF")]
    [TestCase("", "10.250,00 ")]
    public async Task FormatCurrency_AppendsTheCurrencyCodeAsGiven(string currency, string expected)
    {
        (await Render("[[ FormatCurrency subtotal code ]]", new { Subtotal = 10250m, Code = currency })).ShouldBe(expected);
    }

    [Test]
    [SetCulture("en-GB")]
    public async Task FormatCurrency_IgnoresTheMachineCulture()
    {
        (await Render("[[ FormatCurrency total currency ]]")).ShouldBe("10.455,00 EUR");
    }

    [Test]
    public async Task FormatCurrency_AcceptsNullableAmounts()
    {
        (await Render("[[ FormatCurrency monthlyRate currency ]] / [[ FormatCurrency totalExpenses currency ]]"))
            .ShouldBe("500,00 EUR / 120,50 EUR");
    }

    [Test]
    public async Task FormatCurrency_AcceptsIntegerLiterals()
    {
        (await Render("[[ FormatCurrency 1500 'EUR' ]]")).ShouldBe("1.500,00 EUR");
    }

    [Test]
    public async Task FormatCurrency_AcceptsPipeSyntax()
    {
        (await Render("[[ total | FormatCurrency currency ]]")).ShouldBe("10.455,00 EUR");
    }

    [Test]
    public async Task FormatCurrency_AcceptsArithmeticInParentheses()
    {
        (await Render("[[ FormatCurrency (subtotal + totalTax) currency ]]")).ShouldBe("10.455,00 EUR");
    }

    [Test]
    public async Task FormatCurrency_WithMissingCurrency_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Render("[[ FormatCurrency total ]]"));
    }

    [TestCase("en-US", "10,455.00 EUR")]
    [TestCase("de-DE", "10.455,00 EUR")]
    [TestCase("fr-FR", "10 455,00 EUR")]
    [TestCase("de-CH", "10’455.00 EUR")]
    [TestCase("it-IT", "10.455,00 EUR")]
    public async Task FormatCurrency_WithLocale_UsesThatLocale(string locale, string expected)
    {
        (await Render("[[ FormatCurrency total currency loc ]]", MonthlyInvoice(), locale)).ShouldBe(expected);
    }

    [TestCase("''")]
    [TestCase("null")]
    public async Task FormatCurrency_WithEmptyLocale_KeepsItalianDefault(string locale)
    {
        (await Render($"[[ FormatCurrency total currency {locale} ]]")).ShouldBe("10.455,00 EUR");
    }

    [Test]
    public async Task FormatCurrency_WithLocale_AcceptsPipeSyntax()
    {
        (await Render("[[ total | FormatCurrency 'USD' 'en-US' ]]")).ShouldBe("10,455.00 USD");
    }

    // ---------- FormatDecimal ----------

    [TestCase(22, 2, "22.00")]
    [TestCase(123.456, 2, "123.46")]
    [TestCase(123.455, 2, "123.46")] // Math.Round rounds half to even (banker's): .455 → .46
    [TestCase(123.445, 2, "123.44")] // ... but .445 → .44, unlike FormatCurrency
    [TestCase(18.5, 0, "18")]
    [TestCase(19.5, 0, "20")]
    [TestCase(0.1, 3, "0.100")]
    [TestCase(-20, 1, "-20.0")]
    public async Task FormatDecimal_RoundsToTheGivenDecimals(decimal value, int decimals, string expected)
    {
        (await Render("[[ FormatDecimal value places ]]", new { Value = value, Places = decimals })).ShouldBe(expected);
    }

    [Test]
    public async Task FormatDecimal_HasNoThousandsSeparator()
    {
        (await Render("[[ FormatDecimal subtotal 2 ]]")).ShouldBe("10250.00");
    }

    [Test]
    public async Task FormatDecimal_AcceptsNullableAndIntegerValues()
    {
        (await Render("[[ FormatDecimal monthlyRate 1 ]] [[ FormatDecimal workedDays 1 ]]")).ShouldBe("500.0 21.0");
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDecimal_UsesTheMachineCulture()
    {
        // Unlike FormatCurrency, FormatDecimal follows the regional settings of the server
        (await Render("[[ FormatDecimal 123.456 2 ]]")).ShouldBe("123,46");
    }

    [TestCase("en-US", "1234.57")]
    [TestCase("it-IT", "1234,57")]
    [TestCase("de-DE", "1234,57")]
    [TestCase("en-GB", "1234.57")]
    public async Task FormatDecimal_WithLocale_UsesThatDecimalSeparator(string locale, string expected)
    {
        (await Render("[[ FormatDecimal 1234.567 2 loc ]]", null, locale)).ShouldBe(expected);
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDecimal_WithLocale_OverridesTheMachineCulture()
    {
        (await Render("[[ for tax in taxes ]][[ FormatDecimal tax.rate 2 'en-US' ]];[[ end ]]"))
            .ShouldBe("22.00;-20.00;");
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDecimal_WithEmptyLocale_KeepsTheMachineCulture()
    {
        (await Render("[[ FormatDecimal 123.456 2 '' ]]")).ShouldBe("123,46");
    }

    // ---------- FormatDate ----------

    [TestCase("dd/MM/yyyy", "30/09/2026")]
    [TestCase("yyyy-MM-dd", "2026-09-30")]
    [TestCase("MMMM dd, yyyy", "September 30, 2026")]
    [TestCase("MMM yyyy", "Sep 2026")]
    [TestCase("dddd", "Wednesday")]
    [TestCase("yy-MM", "26-09")]
    public async Task FormatDate_AppliesDotNetFormat(string format, string expected)
    {
        (await Render("[[ FormatDate date fmt ]]", new { Date = new DateTime(2026, 9, 30), Fmt = format })).ShouldBe(expected);
    }

    [Test]
    public async Task FormatDate_OnInvoiceDateAndDueDate()
    {
        (await Render("[[ FormatDate date \"dd/MM/yyyy\" ]] → [[ FormatDate dueDate \"dd/MM/yyyy\" ]]"))
            .ShouldBe("30/09/2026 → 31/10/2026");
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDate_MonthNames_FollowTheMachineCulture()
    {
        (await Render("[[ FormatDate date 'MMMM yyyy' ]]")).ShouldBe("settembre 2026");
    }

    [Test]
    public async Task FormatDate_WithNullDueDate_RendersDateTimeMinValue()
    {
        // Scriban converts null to default(DateTime): guard with [[ if dueDate ]] (see IfDueDate_IsFalseWhenNull)
        (await Render("[[ FormatDate dueDate 'dd/MM/yyyy' ]]", OneTimeInvoice())).ShouldBe("01/01/0001");
    }

    [TestCase("it-IT", "mercoledì 30 settembre 2026")]
    [TestCase("es-ES", "miércoles 30 septiembre 2026")]
    [TestCase("de-DE", "Mittwoch 30 September 2026")]
    [TestCase("en-US", "Wednesday 30 September 2026")]
    public async Task FormatDate_WithLocale_UsesThatLanguage(string locale, string expected)
    {
        (await Render("[[ FormatDate date 'dddd d MMMM yyyy' loc ]]", MonthlyInvoice(), locale)).ShouldBe(expected);
    }

    [TestCase("it-IT", "30/09/2026")]
    [TestCase("en-US", "9/30/2026")]
    [TestCase("de-DE", "30.09.2026")]
    public async Task FormatDate_WithLocale_ShortDatePatternFollowsTheLocale(string locale, string expected)
    {
        (await Render("[[ FormatDate date 'd' loc ]]", MonthlyInvoice(), locale)).ShouldBe(expected);
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDate_WithLocale_OverridesTheMachineCulture()
    {
        (await Render("[[ FormatDate dueDate 'MMMM yyyy' 'en-US' ]]")).ShouldBe("October 2026");
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormatDate_WithEmptyLocale_KeepsTheMachineCulture()
    {
        (await Render("[[ FormatDate date 'MMMM' '' ]]")).ShouldBe("settembre");
    }

    // ---------- locale argument ----------

    [Test]
    [SetCulture("en-US")]
    public async Task FormattingFunctions_WithLocaleVariable_UseTheCustomerLocale()
    {
        var result = await Render("[[ FormatDate date 'd MMMM yyyy' locale ]] | [[ FormatCurrency 1234.5 currency locale ]] | [[ FormatDecimal 22.5 2 locale ]]");

        result.ShouldBe("30 settembre 2026 | 1.234,50 EUR | 22,50");
    }

    [Test]
    [SetCulture("it-IT")]
    public async Task FormattingFunctions_WithEnglishCustomerLocale_OverrideTheItalianServer()
    {
        var invoice = MonthlyInvoice() with { Locale = "en-US" };

        var result = await Render("[[ FormatDate date 'd MMMM yyyy' locale ]] | [[ FormatCurrency 1234.5 currency locale ]] | [[ FormatDecimal 22.5 2 locale ]]", invoice);

        result.ShouldBe("30 September 2026 | 1,234.50 EUR | 22.50");
    }

    [Test]
    public async Task LocalizeFunctions_WithLocaleVariable_PassTheCustomerLocale()
    {
        _localization.Setup(l => l.GetMonthName(9, "it-IT", false)).Returns("Settembre");
        _localization.Setup(l => l.GetDayOfWeek(DayOfWeek.Wednesday, "it-IT", false)).Returns("Mercoledì");

        (await Render("[[ LocalizeDayOfWeek date locale ]] [[ LocalizeMonth monthNumber locale ]]")).ShouldBe("Mercoledì Settembre");
    }

    [TestCase("[[ FormatCurrency total currency 'xx-NOPE' ]]")]
    [TestCase("[[ FormatDecimal total 2 'xx-NOPE' ]]")]
    [TestCase("[[ FormatDate date 'd' 'xx-NOPE' ]]")]
    public async Task FormattingFunctions_WithUnknownLocale_FailWithTheLocaleInTheMessage(string template)
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Render(template));

        ex.InnerException.ShouldNotBeNull();
        ex.InnerException.ToString().ShouldContain("Unknown locale 'xx-NOPE'");
    }

    [Test]
    public async Task FormattingFunctions_WithTooManyArguments_Throw()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Render("[[ FormatCurrency total currency 'it-IT' 'extra' ]]"));
    }

    // ---------- LocalizeMonth / LocalizeDayOfWeek ----------

    [Test]
    public async Task LocalizeMonth_PassesMonthLocaleAndShortFlagToLocalizationService()
    {
        _localization.Setup(l => l.GetMonthName(9, "it-IT", false)).Returns("Settembre");
        _localization.Setup(l => l.GetMonthName(9, "it-IT", true)).Returns("Set");

        (await Render("[[ LocalizeMonth monthNumber 'it-IT' false ]] / [[ LocalizeMonth monthNumber 'it-IT' true ]]"))
            .ShouldBe("Settembre / Set");
    }

    [Test]
    public async Task LocalizeDayOfWeek_PassesDayLocaleAndShortFlagToLocalizationService()
    {
        _localization.Setup(l => l.GetDayOfWeek(DayOfWeek.Wednesday, "es-ES", false)).Returns("Miércoles");
        _localization.Setup(l => l.GetDayOfWeek(DayOfWeek.Wednesday, "es-ES", true)).Returns("Mié");

        (await Render("[[ LocalizeDayOfWeek date 'es-ES' false ]] / [[ LocalizeDayOfWeek date 'es-ES' true ]]"))
            .ShouldBe("Miércoles / Mié");
    }

    [Test]
    public async Task LocalizeMonth_WithoutShortFlag_UsesFullName()
    {
        // The editor palette inserts the two-argument form
        _localization.Setup(l => l.GetMonthName(9, "it-IT", false)).Returns("Settembre");

        (await Render("[[ LocalizeMonth monthNumber 'it-IT' ]]")).ShouldBe("Settembre");
    }

    [Test]
    public async Task LocalizeDayOfWeek_WithoutShortFlag_UsesFullName()
    {
        _localization.Setup(l => l.GetDayOfWeek(DayOfWeek.Wednesday, "it-IT", false)).Returns("Mercoledì");

        (await Render("[[ LocalizeDayOfWeek date 'it-IT' ]]")).ShouldBe("Mercoledì");
    }

    [Test]
    public async Task LocalizeFunctions_WithRealLocalizationFiles_ResolveItalianNames()
    {
        var imageRepository = new Mock<IRepository<ImageAsset>>();
        imageRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var renderer = new ScribanTemplateRenderer(
            Mock.Of<ILogger<ScribanTemplateRenderer>>(), new LocalizationService(), imageRepository.Object);

        var result = await renderer.RenderAsync(
            "[[ LocalizeDayOfWeek date 'it-IT' false ]] [[ LocalizeMonth monthNumber 'it-IT' false ]]",
            MonthlyInvoice(),
            TestContext.CurrentContext.CancellationToken);

        result.ShouldBe("Mercoledì Settembre");
    }

    // ---------- Image ----------

    [Test]
    public async Task Image_RendersDataUriWithOptionalSize()
    {
        _images.Add(new ImageAsset("companyLogo", "logo.png", "image/png", "QUJD", 3));

        (await Render("[[ Image 'companyLogo' 200 80 ]]"))
            .ShouldBe("<img src=\"data:image/png;base64,QUJD\" width=\"200\" height=\"80\" alt=\"companyLogo\" />");
        (await Render("[[ Image 'COMPANYLOGO' ]]"))
            .ShouldBe("<img src=\"data:image/png;base64,QUJD\" alt=\"COMPANYLOGO\" />");
    }

    [Test]
    public async Task Image_WithUnknownAlias_RendersComment()
    {
        (await Render("[[ Image 'missing' ]]")).ShouldBe("<!-- Image 'missing' not found -->");
    }

    // ---------- the editor palette, end to end ----------

    [Test]
    public async Task EditorSnippets_RenderAgainstAMonthlyInvoiceWithoutErrors()
    {
        // The Loops & conditions snippets of templateVariables.ts, as inserted
        const string template = """
            [[ for item in lineItems ]]
            <tr>
              <td>[[ item.description ]]</td>
              <td>[[ item.quantity ]]</td>
              <td>[[ FormatCurrency item.amount currency ]]</td>
            </tr>
            [[ end ]]
            [[ for tax in taxes ]]
            <tr>
              <td>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)</td>
              <td>[[ FormatCurrency tax.amount currency ]]</td>
            </tr>
            [[ end ]]
            [[ for day in workedDayItems ]]
              [[ day.date ]] [[ day.hours ]]h
            [[ end ]]
            [[ for p in projectSummary ]]
              [[ p.name ]]: [[ p.workedDays ]] days, [[ p.totalHours ]]h, [[ p.amount ]]
            [[ end ]]
            [[ if workedDays ]]
              Worked [[ workedDays ]] days in [[ monthDescription ]]
            [[ end ]]
            Invoice [[ invoiceNumber ]] of [[ FormatDate date "dd/MM/yyyy" ]] total [[ FormatCurrency total currency ]]
            """;

        var result = await Render(template);

        result.ShouldContain("<td>Trade reporting gateway</td>");
        result.ShouldContain("<td>9.250,00 EUR</td>");
        result.ShouldContain("<td>VAT (22.00%)</td>");
        result.ShouldContain("01/09/2026 8h");
        result.ShouldContain("FIX certification: 2 days, 16h, 1000");
        result.ShouldContain("Worked 21 days in September 2026");
        result.ShouldContain("Invoice 26-09-004 of 30/09/2026 total 10.455,00 EUR");
    }
}
