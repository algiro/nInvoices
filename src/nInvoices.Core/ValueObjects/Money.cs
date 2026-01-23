namespace nInvoices.Core.ValueObjects;

/// <summary>
/// Represents a monetary value with currency.
/// Immutable value object following DDD principles.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));
        
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be ISO 4217 code (3 letters)", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException(
                $"Cannot add different currencies: {left.Currency} and {right.Currency}");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException(
                $"Cannot subtract different currencies: {left.Currency} and {right.Currency}");

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier, money.Currency);

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("Cannot divide money by zero");

        return new Money(money.Amount / divisor, money.Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    public bool IsNegative => Amount < 0;
    public bool IsPositive => Amount > 0;
    public bool IsZero => Amount == 0;

    public override string ToString() => $"{Amount:N2} {Currency}";
}
