using nInvoices.Core.ValueObjects;
using NUnit.Framework;
using Shouldly;

namespace nInvoices.Core.Tests.ValueObjects;

[TestFixture]
public sealed class MoneyTests
{
    [Test]
    public void Constructor_WithValidValues_CreatesInstance()
    {
        var money = new Money(100.50m, "EUR");

        money.Amount.ShouldBe(100.50m);
        money.Currency.ShouldBe("EUR");
    }

    [Test]
    public void Constructor_WithLowercaseCurrency_ConvertsToUppercase()
    {
        var money = new Money(100m, "eur");

        money.Currency.ShouldBe("EUR");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidCurrency_ThrowsException(string? currency)
    {
        Should.Throw<ArgumentException>(() => new Money(100m, currency!));
    }

    [TestCase("US")]
    [TestCase("EURO")]
    public void Constructor_WithInvalidCurrencyLength_ThrowsException(string currency)
    {
        Should.Throw<ArgumentException>(() => new Money(100m, currency));
    }

    [Test]
    public void Addition_WithSameCurrency_ReturnsSummed()
    {
        var money1 = new Money(100m, "EUR");
        var money2 = new Money(50m, "EUR");

        var result = money1 + money2;

        result.Amount.ShouldBe(150m);
        result.Currency.ShouldBe("EUR");
    }

    [Test]
    public void Addition_WithDifferentCurrency_ThrowsException()
    {
        var money1 = new Money(100m, "EUR");
        var money2 = new Money(50m, "USD");

        Should.Throw<InvalidOperationException>(() => money1 + money2);
    }

    [Test]
    public void Multiplication_WithMultiplier_ReturnsMultiplied()
    {
        var money = new Money(100m, "EUR");

        var result = money * 2.5m;

        result.Amount.ShouldBe(250m);
    }

    [Test]
    public void ToString_ReturnsFormattedString()
    {
        var money = new Money(1234.56m, "EUR");

        var result = money.ToString();

        result.ShouldBe("1,234.56 EUR");
    }
}
