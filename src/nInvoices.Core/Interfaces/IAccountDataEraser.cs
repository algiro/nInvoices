namespace nInvoices.Core.Interfaces;

/// <summary>Removes everything a user stored, for account deletion.</summary>
public interface IAccountDataEraser
{
    /// <summary>
    /// Deletes all of the user's data (customers, invoices, worked days, templates, settings,
    /// e-invoicing records...) and destroys their encryption key. Safe to run again.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteAllAsync(string userId, CancellationToken cancellationToken = default);
}
