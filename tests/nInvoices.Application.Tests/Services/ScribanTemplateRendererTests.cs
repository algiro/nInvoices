using Microsoft.Extensions.Logging;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

[TestFixture]
public sealed class ScribanTemplateRendererTests
{
    private ScribanTemplateRenderer _renderer = null!;

    [SetUp]
    public void Setup()
    {
        var logger = new Mock<ILogger<ScribanTemplateRenderer>>();
        var localizationService = new Mock<ILocalizationService>();
        var imageAssetRepo = new Mock<IRepository<ImageAsset>>();
        imageAssetRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageAsset>());
        _renderer = new ScribanTemplateRenderer(logger.Object, localizationService.Object, imageAssetRepo.Object);
    }

    [Test]
    public async Task RenderAsync_WithSimplePlaceholder_ReturnsRenderedText()
    {
        // Arrange
        var template = "<h1>Hello [[ name ]]!</h1>";
        var model = new { Name = "World" };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldBe("<h1>Hello World!</h1>");
    }

    [Test]
    public async Task RenderAsync_WithNestedObject_ReturnsRenderedText()
    {
        // Arrange
        var template = "<p>[[ customer.name ]] - [[ customer.email ]]</p>";
        var model = new
        {
            Customer = new { Name = "John Doe", Email = "john@example.com" }
        };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldBe("<p>John Doe - john@example.com</p>");
    }

    [Test]
    public async Task RenderAsync_WithLoop_ReturnsRenderedList()
    {
        // Arrange
        var template = @"
[[ for item in items ]]
- [[ item.name ]]: [[ item.price ]]
[[ end ]]";
        var model = new
        {
            Items = new[]
            {
                new { Name = "Apple", Price = 1.50m },
                new { Name = "Banana", Price = 0.75m }
            }
        };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldContain("- Apple: 1.50");
        result.ShouldContain("- Banana: 0.75");
    }

    [Test]
    public async Task RenderAsync_WithConditional_RendersCorrectBranch()
    {
        // Arrange
        var template = @"
[[ if isPremium ]]
Premium Customer
[[ else ]]
Standard Customer
[[ end ]]";
        var model = new { IsPremium = true };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldContain("Premium Customer");
        result.ShouldNotContain("Standard Customer");
    }

    [Test]
    public async Task RenderAsync_WithFormatCurrencyFunction_FormatsCorrectly()
    {
        // Arrange
        var template = "Total: [[ FormatCurrency amount currency ]]";
        var model = new { Amount = 123.456m, Currency = "EUR" };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldBe("Total: 123.46 EUR");
    }

    [Test]
    public async Task RenderAsync_WithFormatDateFunction_FormatsCorrectly()
    {
        // Arrange
        var template = "Date: [[ FormatDate date 'yyyy-MM-dd' ]]";
        var model = new { Date = new DateTime(2024, 1, 23) };

        // Act
        var result = await _renderer.RenderAsync(template, model);

        // Assert
        result.ShouldBe("Date: 2024-01-23");
    }

    [Test]
    public async Task RenderAsync_WithInvalidSyntax_ThrowsInvalidOperationException()
    {
        // Arrange - Scriban is lenient, but missing 'end' tags should cause errors
        var template = "[[ for item in items ]]no end tag";
        var model = new { Items = new[] { new { Name = "Test" } } };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => 
            _renderer.RenderAsync(template, model));
    }

    [Test]
    public async Task RenderAsync_WithEmptyTemplate_ThrowsArgumentException()
    {
        // Arrange
        var template = "";
        var model = new { };

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => 
            _renderer.RenderAsync(template, model));
    }

    [Test]
    public async Task RenderAsync_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange
        var template = "Hello";

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => 
            _renderer.RenderAsync(template, null!));
    }

    [Test]
    public async Task ValidateAsync_WithValidTemplate_ReturnsSuccess()
    {
        // Arrange
        var template = "<h1>Hello [[ name ]]!</h1>";

        // Act
        var result = await _renderer.ValidateAsync(template);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ValidateAsync_WithInvalidSyntax_ReturnsErrors()
    {
        // Arrange - Missing 'end' for 'for' loop
        var template = "[[ for item in items ]]no end tag";

        // Act
        var result = await _renderer.ValidateAsync(template);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldContain("Line");
    }

    [Test]
    public async Task ValidateAsync_WithEmptyTemplate_ReturnsError()
    {
        // Arrange
        var template = "";

        // Act
        var result = await _renderer.ValidateAsync(template);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("empty"));
    }

    [Test]
    public async Task ValidateAsync_WithComplexTemplate_ValidatesCorrectly()
    {
        // Arrange
        var template = @"
<!DOCTYPE html>
<html>
<body>
    [[ for item in items ]]
    <div>[[ item.name ]]</div>
    [[ end ]]
</body>
</html>";

        // Act
        var result = await _renderer.ValidateAsync(template);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
