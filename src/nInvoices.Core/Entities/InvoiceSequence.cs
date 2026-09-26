namespace nInvoices.Core.Entities;

/// <summary>
/// Represents a user's invoice sequence counter.
/// Ensures unique invoice numbers across all of that user's customers and invoice types.
/// There is at most one record per owner.
/// </summary>
public sealed class InvoiceSequence : OwnedEntityBase
{
    /// <summary>
    /// The current sequence number.
    /// Incremented atomically when generating new invoices.
    /// </summary>
    public int CurrentValue { get; private set; }

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
}
