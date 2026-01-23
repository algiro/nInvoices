using nInvoices.Infrastructure.TaxHandlers;
using NUnit.Framework;
using Shouldly;

namespace nInvoices.Infrastructure.Tests.TaxHandlers;

[TestFixture]
public sealed class FixedAmountTaxHandlerTests
{
    private FixedAmountTaxHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new FixedAmountTaxHandler();
    }

    [Test]
    public void HandlerId_ShouldBeFIXED()
    {
        _handler.HandlerId.ShouldBe("FIXED");
    }

    [Test]
    public void Description_ShouldNotBeEmpty()
    {
        _handler.Description.ShouldNotBeNullOrEmpty();
    }

    [TestCase(100, 50, 50)]
    [TestCase(1000, 50, 50)]
    [TestCase(0, 50, 50)]
    public void Calculate_ReturnsFixedAmount_RegardlessOfBaseAmount(
        decimal baseAmount, 
        decimal fixedAmount, 
        decimal expected)
    {
        var result = _handler.Calculate(baseAmount, fixedAmount);

        result.ShouldBe(expected);
    }

    [Test]
    public void Calculate_WithZeroFixedAmount_ReturnsZero()
    {
        var result = _handler.Calculate(100, 0);

        result.ShouldBe(0);
    }

    [Test]
    public void Calculate_WithNegativeFixedAmount_ThrowsException()
    {
        Should.Throw<ArgumentException>(() => _handler.Calculate(100, -50));
    }

    [Test]
    public void Calculate_WithDecimalFixedAmount_ReturnsExactAmount()
    {
        var result = _handler.Calculate(100, 12.34m);

        result.ShouldBe(12.34m);
    }
}
