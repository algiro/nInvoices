using nInvoices.Infrastructure.TemplateEngine;
using NUnit.Framework;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.TemplateEngine;

[TestFixture]
public sealed class HandlebarsTemplateEngineTests
{
    private HandlebarsTemplateEngine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new HandlebarsTemplateEngine();
    }

    #region Render Tests

    [Test]
    public void Render_WithSimplePlaceholder_ReplacesValue()
    {
        var template = "Hello {{Name}}!";
        var variables = new Dictionary<string, object>
        {
            ["Name"] = "John"
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Hello John!");
    }

    [Test]
    public void Render_WithMultiplePlaceholders_ReplacesAll()
    {
        var template = "{{FirstName}} {{LastName}} is {{Age}} years old";
        var variables = new Dictionary<string, object>
        {
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["Age"] = 30
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("John Doe is 30 years old");
    }

    [Test]
    public void Render_WithNestedProperty_ResolvesCorrectly()
    {
        var template = "Customer: {{Customer.Name}}";
        var customer = new { Name = "Acme Corp", FiscalId = "12345" };
        var variables = new Dictionary<string, object>
        {
            ["Customer"] = customer
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Customer: Acme Corp");
    }

    [Test]
    public void Render_WithDeepNestedProperty_ResolvesCorrectly()
    {
        var template = "City: {{Customer.Address.City}}";
        var customer = new
        {
            Name = "Acme Corp",
            Address = new { City = "New York", ZipCode = "10001" }
        };
        var variables = new Dictionary<string, object>
        {
            ["Customer"] = customer
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("City: New York");
    }

    [Test]
    public void Render_WithLocalization_UsesCorrectLanguage()
    {
        var template = "Month: {{MonthDescription.EN}}";
        var monthDescriptions = new Dictionary<string, object>
        {
            ["EN"] = "January",
            ["ES"] = "Enero",
            ["IT"] = "Gennaio"
        };
        var variables = new Dictionary<string, object>
        {
            ["MonthDescription"] = monthDescriptions
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Month: January");
    }

    [Test]
    public void Render_WithMissingVariable_KeepsPlaceholder()
    {
        var template = "Hello {{Name}}!";
        var variables = new Dictionary<string, object>();

        var result = _engine.Render(template, variables);

        result.ShouldBe("Hello {{Name}}!");
    }

    [Test]
    public void Render_WithDecimalValue_FormatsCorrectly()
    {
        var template = "Total: {{Total}}";
        var variables = new Dictionary<string, object>
        {
            ["Total"] = 1234.5678m
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Total: 1,234.57");
    }

    [Test]
    public void Render_WithDateValue_FormatsCorrectly()
    {
        var template = "Date: {{Date}}";
        var variables = new Dictionary<string, object>
        {
            ["Date"] = new DateTime(2026, 1, 23)
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Date: 2026-01-23");
    }

    [Test]
    public void Render_WithDateOnlyValue_FormatsCorrectly()
    {
        var template = "Date: {{Date}}";
        var variables = new Dictionary<string, object>
        {
            ["Date"] = new DateOnly(2026, 1, 23)
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Date: 2026-01-23");
    }

    [Test]
    public void Render_WithNullValue_ReturnsEmpty()
    {
        var template = "Value: {{Value}}";
        var variables = new Dictionary<string, object>
        {
            ["Value"] = null!
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Value: ");
    }

    [Test]
    public void Render_WithWhitespaceInPlaceholder_TrimsAndResolves()
    {
        var template = "Hello {{ Name }}!";
        var variables = new Dictionary<string, object>
        {
            ["Name"] = "John"
        };

        var result = _engine.Render(template, variables);

        result.ShouldBe("Hello John!");
    }

    [Test]
    public void Render_WithNullTemplate_ThrowsException()
    {
        Should.Throw<ArgumentNullException>(() => 
            _engine.Render(null!, new Dictionary<string, object>()));
    }

    [Test]
    public void Render_WithNullVariables_ThrowsException()
    {
        Should.Throw<ArgumentNullException>(() => 
            _engine.Render("{{Name}}", null!));
    }

    #endregion

    #region ExtractPlaceholders Tests

    [Test]
    public void ExtractPlaceholders_WithSinglePlaceholder_ReturnsOne()
    {
        var template = "Hello {{Name}}!";

        var placeholders = _engine.ExtractPlaceholders(template).ToList();

        placeholders.ShouldContain("Name");
        placeholders.Count.ShouldBe(1);
    }

    [Test]
    public void ExtractPlaceholders_WithMultiplePlaceholders_ReturnsAll()
    {
        var template = "{{FirstName}} {{LastName}} is {{Age}} years old";

        var placeholders = _engine.ExtractPlaceholders(template).ToList();

        placeholders.Count.ShouldBe(3);
        placeholders.ShouldContain("FirstName");
        placeholders.ShouldContain("LastName");
        placeholders.ShouldContain("Age");
    }

    [Test]
    public void ExtractPlaceholders_WithDuplicates_ReturnsUnique()
    {
        var template = "{{Name}} and {{Name}} again";

        var placeholders = _engine.ExtractPlaceholders(template).ToList();

        placeholders.Count.ShouldBe(1);
        placeholders.ShouldContain("Name");
    }

    [Test]
    public void ExtractPlaceholders_WithNestedProperties_ReturnsFullPath()
    {
        var template = "{{Customer.Name}} from {{Customer.Address.City}}";

        var placeholders = _engine.ExtractPlaceholders(template).ToList();

        placeholders.Count.ShouldBe(2);
        placeholders.ShouldContain("Customer.Name");
        placeholders.ShouldContain("Customer.Address.City");
    }

    [Test]
    public void ExtractPlaceholders_WithNoPlaceholders_ReturnsEmpty()
    {
        var template = "Hello World!";

        var placeholders = _engine.ExtractPlaceholders(template);

        placeholders.ShouldBeEmpty();
    }

    [Test]
    public void ExtractPlaceholders_WithNullTemplate_ThrowsException()
    {
        Should.Throw<ArgumentNullException>(() => 
            _engine.ExtractPlaceholders(null!));
    }

    #endregion

    #region ValidateTemplate Tests

    [Test]
    public void ValidateTemplate_WithValidTemplate_ReturnsTrue()
    {
        var template = "Hello {{Name}}! You are {{Age}} years old.";

        var isValid = _engine.ValidateTemplate(template, out var errors);

        isValid.ShouldBeTrue();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void ValidateTemplate_WithUnmatchedOpenBrace_ReturnsFalse()
    {
        var template = "Hello {{Name}! Missing closing brace";

        var isValid = _engine.ValidateTemplate(template, out var errors);

        isValid.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Unmatched placeholder braces"));
    }

    [Test]
    public void ValidateTemplate_WithUnmatchedCloseBrace_ReturnsFalse()
    {
        var template = "Hello {Name}}! Extra closing brace";

        var isValid = _engine.ValidateTemplate(template, out var errors);

        isValid.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Unmatched placeholder braces"));
    }

    [Test]
    public void ValidateTemplate_WithEmptyPlaceholder_ReturnsFalse()
    {
        var template = "Hello {{}}! Empty placeholder";

        var isValid = _engine.ValidateTemplate(template, out var errors);

        isValid.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Empty placeholder"));
    }

    [Test]
    public void ValidateTemplate_WithNestedPlaceholders_ReturnsFalse()
    {
        var template = "Hello {{Name {{Inner}}}}!";

        var isValid = _engine.ValidateTemplate(template, out var errors);

        isValid.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Nested placeholders"));
    }

    [Test]
    public void ValidateTemplate_WithNullTemplate_ThrowsException()
    {
        Should.Throw<ArgumentNullException>(() => 
            _engine.ValidateTemplate(null!, out _));
    }

    #endregion
}
