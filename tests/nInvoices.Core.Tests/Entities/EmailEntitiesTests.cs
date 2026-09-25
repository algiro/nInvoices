using nInvoices.Core.Entities;
using nInvoices.Core.ValueObjects;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class EmailEntitiesTests
{
    [TestCase("", "Subject", "<p>Body</p>")]
    [TestCase("Name", " ", "<p>Body</p>")]
    [TestCase("Name", "Subject", "")]
    public void EmailTemplate_RequiresNameSubjectAndBody(string name, string subject, string body)
    {
        Should.Throw<ArgumentException>(() => new EmailTemplate(1, name, subject, body));
    }

    [Test]
    public void EmailTemplate_StartsInactive()
    {
        new EmailTemplate(1, "Standard", "Invoice [[ invoiceNumber ]]", "<p>Hi</p>").IsActive.ShouldBeFalse();
    }

    [Test]
    public void OAuthState_ExpiresAfterItsLifetime()
    {
        var created = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var state = new OAuthState("s", "user", created);

        state.IsExpired(created.Add(OAuthState.Lifetime).AddSeconds(-1)).ShouldBeFalse();
        state.IsExpired(created.Add(OAuthState.Lifetime)).ShouldBeTrue();
    }

    [Test]
    public void GmailConnection_ReconnectReplacesTokenAndConnectionTime()
    {
        var connection = new GmailConnection("user", "old@gmail.com", "enc-old", "scope");
        var firstConnected = connection.ConnectedAt;

        connection.Reconnect("new@gmail.com", "enc-new", "scope");

        connection.EmailAddress.ShouldBe("new@gmail.com");
        connection.EncryptedRefreshToken.ShouldBe("enc-new");
        connection.ConnectedAt.ShouldBeGreaterThanOrEqualTo(firstConnected);
    }

    [Test]
    public void Customer_SetContact_StoresTrimmedValuesAndNullForBlank()
    {
        var customer = new Customer("ACME", "IT1", new Address("Via Roma", "1", "Milano", "20121", "Italy"));

        customer.SetContact("  billing@acme.it ", "  ");

        customer.Email.ShouldBe("billing@acme.it");
        customer.CcEmails.ShouldBeNull();
    }
}
