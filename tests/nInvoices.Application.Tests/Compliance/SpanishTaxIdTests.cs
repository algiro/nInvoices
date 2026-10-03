using nInvoices.Application.Compliance.Spain;
using Shouldly;

namespace nInvoices.Application.Tests.Compliance;

[TestFixture]
public sealed class SpanishTaxIdTests
{
    [TestCase("12345678Z")]      // DNI
    [TestCase("12345678-Z")]
    [TestCase("12.345.678 z")]
    [TestCase("ES12345678Z")]
    [TestCase("X1234567L")]      // NIE
    [TestCase("K1234567L")]      // K, L, M
    [TestCase("B12345674")]      // CIF, control digit (B requires a digit)
    [TestCase("A58818501")]
    [TestCase("Q2826000H")]      // CIF, control letter (Q requires a letter)
    public void IsValid_ValidIds_ReturnsTrue(string id) =>
        SpanishTaxId.IsValid(id).ShouldBeTrue();

    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345678A")]      // wrong letter
    [TestCase("1234567Z")]       // too short
    [TestCase("X1234567A")]      // wrong NIE letter
    [TestCase("B12345675")]      // wrong control digit
    [TestCase("B1234567D")]      // B cannot use a letter
    [TestCase("Q28260008")]      // Q cannot use a digit
    [TestCase("I12345674")]      // not an entity letter
    [TestCase("IT12345678901")]  // another country
    public void IsValid_InvalidIds_ReturnsFalse(string? id) =>
        SpanishTaxId.IsValid(id).ShouldBeFalse();

    [Test]
    public void Normalize_StripsSeparatorsAndCountryPrefix() =>
        SpanishTaxId.Normalize(" es-12.345.678 z ").ShouldBe("12345678Z");
}
