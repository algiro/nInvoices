using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Services;

/// <summary>
/// The current user's invoice numbers. A draft does not use up a number: it shows the next one
/// (<see cref="PeekAsync"/>), and the number is only taken from the sequence when the invoice is
/// finalized (<see cref="TakeAsync"/>).
/// </summary>
public interface IInvoiceNumbering
{
    /// <summary>
    /// The number the next finalized invoice gets, formatted for this customer and issue date,
    /// without advancing the sequence. Every draft shows this.
    /// </summary>
    Task<InvoiceNumber> PeekAsync(Customer customer, DateOnly issueDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the next number from the sequence for an invoice being finalized and advances the
    /// sequence. The change is only staged: it is saved with the caller's unit of work, together
    /// with the invoice, so a number is never used up without an invoice holding it.
    /// </summary>
    Task<InvoiceNumber> TakeAsync(Customer customer, DateOnly issueDate, CancellationToken cancellationToken = default);
}

public sealed class InvoiceNumbering : IInvoiceNumbering
{
    private readonly IRepository<InvoiceSequence> _sequenceRepository;
    private readonly InvoiceSettings _settings;

    public InvoiceNumbering(IRepository<InvoiceSequence> sequenceRepository, IOptions<InvoiceSettings> settings)
    {
        _sequenceRepository = sequenceRepository;
        _settings = settings.Value;
    }

    public async Task<InvoiceNumber> PeekAsync(Customer customer, DateOnly issueDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        // The query filter returns only the current user's row
        var sequence = (await _sequenceRepository.GetAllAsync(cancellationToken)).FirstOrDefault();

        return Format(sequence?.NumberFormat, customer, sequence?.CurrentValue ?? 1, issueDate);
    }

    public async Task<InvoiceNumber> TakeAsync(Customer customer, DateOnly issueDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var sequence = (await _sequenceRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (sequence == null)
        {
            sequence = new InvoiceSequence(1);
            var number = Format(sequence.NumberFormat, customer, sequence.Increment(), issueDate);
            await _sequenceRepository.AddAsync(sequence, cancellationToken);
            return number;
        }

        var taken = Format(sequence.NumberFormat, customer, sequence.Increment(), issueDate);
        await _sequenceRepository.UpdateAsync(sequence, cancellationToken);
        return taken;
    }

    /// <param name="userFormat">The user's own pattern; null to use the default from the configuration.</param>
    private InvoiceNumber Format(string? userFormat, Customer customer, int sequenceNumber, DateOnly issueDate)
    {
        var pattern = userFormat ?? _settings.NumberFormat;
        var date = issueDate.ToDateTime(TimeOnly.MinValue);

        return InvoiceNumber.Generate(pattern, sequenceNumber, date, customer.FiscalId);
    }
}
