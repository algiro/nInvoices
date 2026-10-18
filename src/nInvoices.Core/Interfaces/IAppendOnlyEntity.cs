namespace nInvoices.Core.Interfaces;

/// <summary>
/// An entity whose rows, once saved, can be neither modified nor deleted, whoever asks (a deleted invoice,
/// a stray update, a bug): records a country's rules keep as evidence, such as a hash chain of invoices.
/// The DbContext refuses such a save.
/// </summary>
public interface IAppendOnlyEntity;
