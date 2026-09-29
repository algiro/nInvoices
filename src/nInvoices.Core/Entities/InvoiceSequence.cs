namespace nInvoices.Core.Entities;

/// <summary>
/// A user's invoice numbering: the sequence counter and, optionally, the pattern the numbers
/// are formatted with. Ensures unique invoice numbers across all of that user's customers and
/// invoice types. There is at most one record per owner, created on first use.
/// </summary>
public sealed class InvoiceSequence : OwnedEntityBase
{
    /// <summary>
    /// The current sequence number.
    /// Incremented atomically when generating new invoices.
    /// </summary>
    public int CurrentValue { get; private set; }

    /// <summary>
    /// Pattern for this user's invoice numbers (see <see cref="ValueObjects.InvoiceNumber"/>);
    /// null to use the default pattern from the configuration.
    /// </summary>
    public string? NumberFormat { get; private set; }

    private InvoiceSequence() 
    { 
        CurrentValue = 1;
    }

    public InvoiceSequence(int initialValue)
    {
        if (initialValue < 1)
            throw new ArgumentException("Sequence value must be at least 1", nameof(initialValue));

        CurrentValue = initialValue;
    }

    /// <summary>
    /// Increments the sequence and returns the current value before incrementing.
    /// This ensures that if CurrentValue is 1, the first invoice gets number 1.
    /// </summary>
    public int Increment()
    {
        var currentValue = CurrentValue;
        CurrentValue++;
        return currentValue;
    }

    /// <summary>
    /// Sets the sequence to a specific value.
    /// Use with caution - can cause duplicate invoice numbers if set too low.
    /// </summary>
    public void SetValue(int value)
    {
        if (value < 1)
            throw new ArgumentException("Sequence value must be at least 1", nameof(value));

        CurrentValue = value;
    }

    /// <param name="pattern">A valid pattern, or null/blank to use the default one.</param>
    /// <exception cref="ArgumentException">The pattern has no token.</exception>
    public void SetNumberFormat(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            NumberFormat = null;
            return;
        }

        if (!ValueObjects.InvoiceNumber.ValidatePattern(pattern))
            throw new ArgumentException("The pattern must contain at least one token, e.g. {NUMBER:000}");

        NumberFormat = pattern.Trim();
    }
}
