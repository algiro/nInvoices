using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class ScopedTemplatesTests
{
    private static InvoiceTemplate Template(long? customerId, string name) =>
        new(customerId, InvoiceType.Monthly, name, "<p>content</p>");

    [Test]
    public void PickEffective_CustomerHasItsOwn_ReturnsIt()
    {
        var shared = Template(null, "shared");
        var own = Template(7, "own");

        ScopedTemplates.PickEffective([shared, own], 7).ShouldBeSameAs(own);
    }

    [Test]
    public void PickEffective_CustomerHasNone_ReturnsTheSharedOne()
    {
        var shared = Template(null, "shared");
        var other = Template(8, "another customer");

        ScopedTemplates.PickEffective([other, shared], 7).ShouldBeSameAs(shared);
    }

    [Test]
    public void PickEffective_NothingApplies_ReturnsNull()
    {
        ScopedTemplates.PickEffective([Template(8, "another customer")], 7).ShouldBeNull();
        ScopedTemplates.PickEffective(Array.Empty<InvoiceTemplate>(), 7).ShouldBeNull();
    }

    [Test]
    public void Constructors_NoCustomer_MakeASharedTemplate()
    {
        Template(null, "shared").CustomerId.ShouldBeNull();
        new EmailTemplate(null, "shared", "subject", "<p>body</p>").CustomerId.ShouldBeNull();
        new MonthlyReportTemplate(null, "shared", "<p>content</p>").CustomerId.ShouldBeNull();
    }

    [Test]
    public void Constructors_NonPositiveCustomer_Throw()
    {
        Should.Throw<ArgumentException>(() => Template(0, "bad"));
        Should.Throw<ArgumentException>(() => new EmailTemplate(-1, "bad", "s", "b"));
        Should.Throw<ArgumentException>(() => new MonthlyReportTemplate(0, "bad", "c"));
    }
}
