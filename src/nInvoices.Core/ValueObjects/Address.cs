namespace nInvoices.Core.ValueObjects;

/// <summary>
/// Represents a physical address.
/// Immutable value object.
/// </summary>
public sealed record Address
{
    public string Street { get; init; }
    public string HouseNumber { get; init; }
    public string City { get; init; }
    public string ZipCode { get; init; }
    public string? State { get; init; }
    public string Country { get; init; }

    public Address(
        string street,
        string houseNumber,
        string city,
        string zipCode,
        string country,
        string? state = null)
    {
        ArgumentNullException.ThrowIfNull(street);
        ArgumentNullException.ThrowIfNull(houseNumber);
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(zipCode);
        ArgumentNullException.ThrowIfNull(country);

        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street cannot be empty", nameof(street));
        if (string.IsNullOrWhiteSpace(houseNumber))
            throw new ArgumentException("House number cannot be empty", nameof(houseNumber));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty", nameof(city));
        if (string.IsNullOrWhiteSpace(zipCode))
            throw new ArgumentException("Zip code cannot be empty", nameof(zipCode));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty", nameof(country));

        Street = street;
        HouseNumber = houseNumber;
        City = city;
        ZipCode = zipCode;
        Country = country;
        State = state;
    }

    public string GetFullAddress() =>
        State is not null
            ? $"{Street} {HouseNumber}, {City}, {State} {ZipCode}, {Country}"
            : $"{Street} {HouseNumber}, {City} {ZipCode}, {Country}";

    public override string ToString() => GetFullAddress();
}
