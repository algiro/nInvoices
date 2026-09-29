using nInvoices.Core.Entities;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class InvoiceSequenceTests
{
    [Test]
    public void Increment_ReturnsTheCurrentValueThenAdvances()
    {
        var sequence = new InvoiceSequence(5);

        sequence.Increment().ShouldBe(5);
        sequence.CurrentValue.ShouldBe(6);
    }

    [TestCase("{NUMBER}")]
    [TestCase("INV-{YEAR}-{NUMBER:000}")]
    [TestCase("{CUSTOMER:3}/{NUMBER:0000}")]
    public void SetNumberFormat_ValidPattern_IsKept(string pattern)
    {
        var sequence = new InvoiceSequence(1);

        sequence.SetNumberFormat($"  {pattern}  ");

        sequence.NumberFormat.ShouldBe(pattern);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void SetNumberFormat_Blank_GoesBackToTheDefault(string? pattern)
    {
        var sequence = new InvoiceSequence(1);
        sequence.SetNumberFormat("X-{NUMBER}");

        sequence.SetNumberFormat(pattern);

        sequence.NumberFormat.ShouldBeNull();
    }

    [Test]
    public void SetNumberFormat_PatternWithoutTokens_ThrowsAndKeepsTheOldOne()
    {
        var sequence = new InvoiceSequence(1);
        sequence.SetNumberFormat("X-{NUMBER}");

        Should.Throw<ArgumentException>(() => sequence.SetNumberFormat("no tokens"));

        sequence.NumberFormat.ShouldBe("X-{NUMBER}");
    }
}
