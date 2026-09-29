using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class RateTests
{
    private static Rate NewRate() => new(1, RateType.Hourly, new Money(50m, "EUR"));

    [Test]
    public void SetName_TrimsIt()
    {
        var rate = NewRate();

        rate.SetName("  Senior developer ");

        rate.Name.ShouldBe("Senior developer");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void SetName_Blank_MeansNoName(string? name)
    {
        var rate = NewRate();
        rate.SetName("Senior");

        rate.SetName(name);

        rate.Name.ShouldBeNull();
    }

    [Test]
    public void SetName_TooLong_ThrowsAndKeepsTheOldName()
    {
        var rate = NewRate();
        rate.SetName("Senior");

        Should.Throw<ArgumentException>(() => rate.SetName(new string('x', Rate.MaxNameLength + 1)));

        rate.Name.ShouldBe("Senior");
    }

    [Test]
    public void SetName_ExactlyTheMaximum_IsAllowed()
    {
        var rate = NewRate();

        rate.SetName(new string('x', Rate.MaxNameLength));

        rate.Name!.Length.ShouldBe(Rate.MaxNameLength);
    }
}
