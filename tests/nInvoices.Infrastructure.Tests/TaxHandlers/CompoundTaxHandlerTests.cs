using nInvoices.Infrastructure.TaxHandlers;
using NUnit.Framework;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.TaxHandlers;

[TestFixture]
public sealed class CompoundTaxHandlerTests
{
    private CompoundTaxHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new CompoundTaxHandler();
    }

    [Test]
    public void HandlerId_ShouldBeCOMPOUND()
    {
        _handler.HandlerId.ShouldBe("COMPOUND");
    }

    [Test]
    public void Description_ShouldNotBeEmpty()
    {
        _handler.Description.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public void Calculate_WithValidContext_ReturnsCorrectAmount()
    {
        var context = new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = 21m // 21% VAT on 100 = 21
        };

        var result = _handler.Calculate(100, 10, context); // 10% on the 21 VAT

        result.ShouldBe(2.1m);
    }

    [Test]
    public void Calculate_WithZeroBaseTax_ReturnsZero()
    {
        var context = new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = 0
        };

        var result = _handler.Calculate(100, 10, context);

        result.ShouldBe(0);
    }

    [Test]
    public void Calculate_WithNullContext_ThrowsException()
    {
        Should.Throw<ArgumentNullException>(() => _handler.Calculate(100, 10, null));
    }

    [Test]
    public void Calculate_WithMissingBaseTaxAmount_ThrowsException()
    {
        var context = new Dictionary<string, decimal>();

        Should.Throw<InvalidOperationException>(() => _handler.Calculate(100, 10, context));
    }

    [Test]
    public void Calculate_WithNegativeRate_ThrowsException()
    {
        var context = new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = 21m
        };

        Should.Throw<ArgumentException>(() => _handler.Calculate(100, -10, context));
    }

    [Test]
    public void Calculate_WithNegativeBaseTaxAmount_ThrowsException()
    {
        var context = new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = -21m
        };

        Should.Throw<ArgumentException>(() => _handler.Calculate(100, 10, context));
    }

    [TestCase(21, 10, 2.1)]
    [TestCase(50, 5, 2.5)]
    [TestCase(100, 15, 15)]
    public void Calculate_WithVariousInputs_ReturnsCorrectPercentage(
        decimal baseTaxAmount,
        decimal rate,
        decimal expected)
    {
        var context = new Dictionary<string, decimal>
        {
            ["BaseTaxAmount"] = baseTaxAmount
        };

        var result = _handler.Calculate(0, rate, context);

        result.ShouldBe(expected);
    }
}
