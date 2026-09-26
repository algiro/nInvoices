using nInvoices.Application.Services.Email;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

[TestFixture]
public sealed class EmailAddressesTests
{
    [TestCase("a@b.it, c@d.com", new[] { "a@b.it", "c@d.com" })]
    [TestCase("a@b.it;c@d.com", new[] { "a@b.it", "c@d.com" })]
    [TestCase(" a@b.it \n c@d.com ", new[] { "a@b.it", "c@d.com" })]
    [TestCase("", new string[0])]
    [TestCase(null, new string[0])]
    public void Split_AcceptsCommonSeparators(string? list, string[] expected)
    {
        EmailAddresses.Split(list).ShouldBe(expected);
    }

    [TestCase("billing@acme.it", true)]
    [TestCase("first.last+tag@sub.example.com", true)]
    [TestCase("no-at-sign", false)]
    [TestCase("user@localhost", false)]
    [TestCase("Name <user@acme.it>", false)]
    public void IsValid_AcceptsPlainAddressesOnly(string address, bool expected)
    {
        EmailAddresses.IsValid(address).ShouldBe(expected);
    }

    [Test]
    public void Normalize_JoinsWithCommaAndReturnsNullWhenEmpty()
    {
        EmailAddresses.Normalize("a@b.it;  c@d.com").ShouldBe("a@b.it, c@d.com");
        EmailAddresses.Normalize("   ").ShouldBeNull();
    }
}
