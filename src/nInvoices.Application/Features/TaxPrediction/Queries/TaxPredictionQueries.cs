using Mediator;
using nInvoices.Application.Countries;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.TaxPrediction;
using nInvoices.Application.Localization;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.TaxPrediction.Queries;

/// <summary>The estimate of the taxes of <paramref name="Year"/>, period by period.</summary>
public sealed record GetTaxPredictionQuery(int Year) : IRequest<TaxPredictionDto>;

/// <summary>The countries whose taxes can be estimated, with the user's profile.</summary>
public sealed record GetTaxPredictionSettingsQuery : IRequest<TaxPredictionSettingsDto>;

public sealed class GetTaxPredictionQueryHandler : IRequestHandler<GetTaxPredictionQuery, TaxPredictionDto>
{
    private readonly ITaxPredictionRegistry _registry;
    private readonly IRepository<TaxProfile> _profiles;
    private readonly IInvoiceRepository _invoices;
    private readonly IRepository<Tax> _taxes;
    private readonly IRepository<BusinessExpense> _expenses;
    private readonly IRepository<RecurringExpense> _recurring;
    private readonly IRepository<TaxPayment> _payments;
    private readonly IUserCountry? _country;
    private readonly IAppText _text;

    public GetTaxPredictionQueryHandler(
        ITaxPredictionRegistry registry,
        IRepository<TaxProfile> profiles,
        IInvoiceRepository invoices,
        IRepository<Tax> taxes,
        IRepository<BusinessExpense> expenses,
        IRepository<RecurringExpense> recurring,
        IRepository<TaxPayment> payments,
        IAppText text,
        IUserCountry? country = null)
    {
        _text = text;
        _country = country;
        _registry = registry;
        _profiles = profiles;
        _invoices = invoices;
        _taxes = taxes;
        _expenses = expenses;
        _recurring = recurring;
        _payments = payments;
    }

    public async ValueTask<TaxPredictionDto> Handle(GetTaxPredictionQuery request, CancellationToken cancellationToken)
    {
        var year = request.Year;

        // The query filter returns only the current user's rows
        var profile = (await _profiles.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (profile is null)
            return TaxPredictionMapper.NotSetUp(year, _text.Translate("taxPrediction.notSetUp.noProfile"));

        // A profile of a country the user no longer works from doesn't apply
        var country = _country is null ? null : await _country.GetAsync(cancellationToken);
        if (country is not null && !string.Equals(country, profile.CountryCode, StringComparison.OrdinalIgnoreCase))
            return TaxPredictionMapper.NotSetUp(year, _text.Translate("taxPrediction.notSetUp.otherCountry"));

        var module = _registry.Find(profile.CountryCode);
        if (module is null || module.Regimes.All(r => r.Id != profile.RegimeId))
            return TaxPredictionMapper.NotSetUp(year, $"Tax estimates for {profile.CountryCode} are not available here: choose again in Settings.");

        // The country's tax year: 6 April to 5 April in the United Kingdom, the calendar year elsewhere
        var taxYear = module.TaxYearOf(year);
        var from = taxYear.Start;
        var to = taxYear.End;
        var invoices = await _invoices.GetIssuedBetweenAsync(from, to, cancellationToken);
        var drafts = await _invoices.FindAsync(i => i.Status == InvoiceStatus.Draft && i.IssueDate >= from && i.IssueDate <= to, cancellationToken);
        var taxes = (await _taxes.GetAllAsync(cancellationToken)).ToList();
        var expenses = (await _expenses.FindAsync(e => e.Date >= from && e.Date <= to, cancellationToken)).ToList();
        var recurring = (await _recurring.GetAllAsync(cancellationToken)).ToList();
        var payments = (await _payments.FindAsync(p => p.Year == year, cancellationToken)).ToList();

        var (input, warnings) = TaxPredictionInputBuilder.Build(module, profile, taxYear, invoices, taxes, expenses, recurring, payments);

        var draftCount = drafts.Count();
        if (draftCount > 0)
            warnings = [.. warnings, $"{draftCount} draft invoice(s) dated {year} are not counted until they are finalized."];

        return TaxPredictionMapper.ToDto(module, taxYear, module.Predict(input), payments, warnings);
    }
}

public sealed class GetTaxPredictionSettingsQueryHandler : IRequestHandler<GetTaxPredictionSettingsQuery, TaxPredictionSettingsDto>
{
    private readonly ITaxPredictionRegistry _registry;
    private readonly IRepository<TaxProfile> _profiles;

    public GetTaxPredictionSettingsQueryHandler(ITaxPredictionRegistry registry, IRepository<TaxProfile> profiles)
    {
        _registry = registry;
        _profiles = profiles;
    }

    public async ValueTask<TaxPredictionSettingsDto> Handle(GetTaxPredictionSettingsQuery request, CancellationToken cancellationToken)
    {
        // The query filter returns only the current user's rows
        var profile = (await _profiles.GetAllAsync(cancellationToken)).FirstOrDefault();

        return new TaxPredictionSettingsDto(
            _registry.Offered.Select(TaxPredictionMapper.ToDto).ToList(),
            profile is null ? null : TaxPredictionMapper.ToDto(profile));
    }
}
