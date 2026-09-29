namespace nInvoices.Core.Interfaces;

/// <summary>
/// A template that belongs either to one customer or, with no customer, is shared by all of the
/// user's customers. Invoice, monthly report and email templates all work this way.
/// </summary>
public interface IScopedTemplate
{
    /// <summary>The customer the template belongs to; null for a shared template.</summary>
    long? CustomerId { get; }

    bool IsActive { get; }
}

public static class ScopedTemplates
{
    /// <summary>
    /// The template that applies to a customer out of its own and the shared ones (already
    /// narrowed to the active ones of the right type): the customer's own wins, otherwise the
    /// shared one. A customer with its own active template never falls back to the shared one.
    /// </summary>
    public static T? PickEffective<T>(IEnumerable<T> candidates, long customerId) where T : class, IScopedTemplate =>
        candidates.Where(t => t.CustomerId == customerId).FirstOrDefault()
        ?? candidates.Where(t => t.CustomerId is null).FirstOrDefault();
}
