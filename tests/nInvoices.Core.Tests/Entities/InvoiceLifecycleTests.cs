using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class InvoiceLifecycleTests
{
    // The whole table: every status × every action, and whether it is allowed
    private static readonly (InvoiceStatus From, InvoiceAction Action)[] Allowed =
    [
        (InvoiceStatus.Draft, InvoiceAction.Finalize),
        (InvoiceStatus.Draft, InvoiceAction.Delete),
        (InvoiceStatus.Finalized, InvoiceAction.MarkAsSent),
        (InvoiceStatus.Finalized, InvoiceAction.MarkAsPaid),
        (InvoiceStatus.Finalized, InvoiceAction.Cancel),
        (InvoiceStatus.Sent, InvoiceAction.MarkAsPaid),
        (InvoiceStatus.Sent, InvoiceAction.Cancel),
    ];

    private static IEnumerable<TestCaseData> EveryStatusAndAction() =>
        from status in Enum.GetValues<InvoiceStatus>()
        from action in Enum.GetValues<InvoiceAction>()
        let allowed = Allowed.Contains((status, action))
        select new TestCaseData(status, action, allowed).SetName($"{status} + {action} → {(allowed ? "allowed" : "refused")}");

    [TestCaseSource(nameof(EveryStatusAndAction))]
    public void WhyNot_FollowsTheLifecycle(InvoiceStatus status, InvoiceAction action, bool allowed)
    {
        var reason = InvoiceLifecycle.WhyNot(status, action);

        if (allowed)
            reason.ShouldBeNull();
        else
            reason.ShouldNotBeNullOrWhiteSpace();
    }

    [TestCaseSource(nameof(EveryStatusAndAction))]
    public void Invoice_AppliesAllowedActionsAndRefusesTheOthersUnchanged(InvoiceStatus status, InvoiceAction action, bool allowed)
    {
        var invoice = InStatus(status);

        var act = () => Run(invoice, action);

        if (allowed)
        {
            act();
            // Deleting removes the invoice (the handler does): the entity's status stays as it was
            invoice.Status.ShouldBe(InvoiceLifecycle.ChangesStatus(action) ? InvoiceLifecycle.Target(action) : status);
        }
        else
        {
            var error = Should.Throw<InvalidOperationException>(act);
            error.Message.ShouldContain(InvoiceLifecycle.WhyNot(status, action)!);
            invoice.Status.ShouldBe(status);
        }
    }

    [Test]
    public void MarkAsPaid_OnADraft_IsRefused()
    {
        // The single-invoice endpoint used to allow it, leaving a paid invoice without a number of its own
        var draft = InStatus(InvoiceStatus.Draft);

        Should.Throw<InvalidOperationException>(draft.MarkAsPaid).Message.ShouldContain("Not finalized yet");
    }

    [TestCase(InvoiceStatus.Draft, InvoiceAction.Cancel, "delete the draft instead")]
    [TestCase(InvoiceStatus.Finalized, InvoiceAction.Delete, "cancel it instead")]
    [TestCase(InvoiceStatus.Sent, InvoiceAction.Delete, "cancel it instead")]
    public void CancelAndDelete_PointToEachOther(InvoiceStatus status, InvoiceAction action, string hint)
    {
        // A draft was never issued, so it goes away; an issued invoice keeps its number on record
        InvoiceLifecycle.WhyNot(status, action).ShouldContain(hint);
    }

    [Test]
    public void Target_OfDelete_IsNotAStatus()
    {
        InvoiceLifecycle.ChangesStatus(InvoiceAction.Delete).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => InvoiceLifecycle.Target(InvoiceAction.Delete));
    }

    [Test]
    public void Finalize_GivesTheInvoiceItsNumber()
    {
        var invoice = InStatus(InvoiceStatus.Draft);

        invoice.Finalize(new InvoiceNumber("26-10-007"));

        invoice.Status.ShouldBe(InvoiceStatus.Finalized);
        invoice.Number.ToString().ShouldBe("26-10-007");
    }

    [Test]
    public void Finalize_Twice_KeepsTheFirstNumber()
    {
        var invoice = InStatus(InvoiceStatus.Draft);
        invoice.Finalize(new InvoiceNumber("26-10-007"));

        Should.Throw<InvalidOperationException>(() => invoice.Finalize(new InvoiceNumber("26-10-008")));

        invoice.Number.ToString().ShouldBe("26-10-007");
    }

    [Test]
    public void RenumberDraft_ChangesTheNumberOfADraft()
    {
        var draft = InStatus(InvoiceStatus.Draft);

        draft.RenumberDraft(new InvoiceNumber("26-10-002"));

        draft.Number.ToString().ShouldBe("26-10-002");
    }

    [TestCase(InvoiceStatus.Finalized)]
    [TestCase(InvoiceStatus.Sent)]
    [TestCase(InvoiceStatus.Paid)]
    [TestCase(InvoiceStatus.Cancelled)]
    public void RenumberDraft_OnAnIssuedInvoice_IsRefused(InvoiceStatus status)
    {
        var invoice = InStatus(status);
        var number = invoice.Number;

        Should.Throw<InvalidOperationException>(() => invoice.RenumberDraft(new InvoiceNumber("26-10-099")));

        invoice.Number.ShouldBe(number);
    }

    [Test]
    public void Totals_AreTheSubtotalPlusExpensesPlusTaxes()
    {
        var invoice = InStatus(InvoiceStatus.Draft);

        invoice.AddExpenses(new Money(42.5m, "EUR"));
        invoice.AddTaxes(new Money(21m, "EUR"));

        invoice.Total.Amount.ShouldBe(163.5m);
    }

    [Test]
    public void RestoreImported_KeepsTheExportedStatusAndAmounts()
    {
        var invoice = InStatus(InvoiceStatus.Draft);

        invoice.RestoreImported(InvoiceStatus.Paid, new Money(0m, "EUR"), new Money(21m, "EUR"), new Money(121m, "EUR"));

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.TotalTaxes.Amount.ShouldBe(21m);
        invoice.Total.Amount.ShouldBe(121m);
    }

    [Test]
    public void RestoreImported_InAnotherCurrency_IsRefused()
    {
        var invoice = InStatus(InvoiceStatus.Draft);

        Should.Throw<ArgumentException>(() =>
            invoice.RestoreImported(InvoiceStatus.Paid, new Money(0m, "EUR"), new Money(21m, "USD"), new Money(121m, "EUR")));

        invoice.Status.ShouldBe(InvoiceStatus.Draft);
    }

    [Test]
    public void RestoreImported_WithAnUnknownStatus_IsRefused()
    {
        var invoice = InStatus(InvoiceStatus.Draft);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            invoice.RestoreImported((InvoiceStatus)42, new Money(0m, "EUR"), new Money(0m, "EUR"), new Money(100m, "EUR")));
    }

    private static Invoice InStatus(InvoiceStatus status)
    {
        var invoice = new Invoice(1, new InvoiceNumber("26-10-001"), InvoiceType.OneTime, new DateOnly(2026, 10, 5), new Money(100m, "EUR"), "EUR");
        if (status == InvoiceStatus.Draft)
            return invoice;

        invoice.Finalize(invoice.Number);
        switch (status)
        {
            case InvoiceStatus.Sent:
                invoice.MarkAsSent();
                break;
            case InvoiceStatus.Paid:
                invoice.MarkAsPaid();
                break;
            case InvoiceStatus.Cancelled:
                invoice.Cancel();
                break;
        }

        return invoice;
    }

    private static void Run(Invoice invoice, InvoiceAction action)
    {
        switch (action)
        {
            case InvoiceAction.Finalize:
                invoice.Finalize(new InvoiceNumber("26-10-050"));
                break;
            case InvoiceAction.MarkAsSent:
                invoice.MarkAsSent();
                break;
            case InvoiceAction.MarkAsPaid:
                invoice.MarkAsPaid();
                break;
            case InvoiceAction.Cancel:
                invoice.Cancel();
                break;
            case InvoiceAction.Delete:
                invoice.EnsureAllowed(InvoiceAction.Delete);
                break;
        }
    }
}
