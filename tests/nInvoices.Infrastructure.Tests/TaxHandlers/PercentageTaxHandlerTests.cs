using nInvoices.Infrastructure.TaxHandlers;
using NUnit.Framework;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.TaxHandlers;

[TestFixture]
public sealed class PercentageTaxHandlerTests
{
    private PercentageTaxHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new PercentageTaxHandler();
    }

    [Test]
    public void HandlerId_ShouldBePERCENTAGE()
    {
        _handler.HandlerId.ShouldBe("PERCENTAGE");
    }

    [Test]
    public void Description_ShouldNotBeEmpty()
    {
        _handler.Description.ShouldNotBeNullOrEmpty();
    }

    [TestCase(100, 21, 21)]
    [TestCase(1000, 10, 100)]
    [TestCase(50.50, 15, 7.575)]
    [TestCase(100, 0, 0)]
    public void Calculate_WithValidInputs_ReturnsCorrectPercentage(
        decimal baseAmount, 
        decimal rate, 
        decimal expected)
    {
        var result = _handler.Calculate(baseAmount, rate);

        result.ShouldBe(expected);
    }

    [Test]
    public void Calculate_WithZeroBaseAmount_ReturnsZero()
    {
        var result = _handler.Calculate(0, 21);

        result.ShouldBe(0);
    }

    [Test]
    public void Calculate_WithNegativeBaseAmount_ThrowsException()
    {
        Should.Throw<ArgumentException>(() => _handler.Calculate(-100, 21));
    }

    [Test]
    public void Calculate_WithNegativeRate_ThrowsException()
    {
        Should.Throw<ArgumentException>(() => _handler.Calculate(100, -21));
    }

    [Test]
    public void Calculate_WithVeryLargeNumbers_HandlesCorrectly()
    {
        var result = _handler.Calculate(1_000_000, 21);

        result.ShouldBe(210_000);
    }

    [Test]
    public void Calculate_WithDecimalPrecision_MaintainsPrecision()
    {
        var result = _handler.Calculate(99.99m, 21.5m);

        result.ShouldBe(21.49785m);
    }
}
