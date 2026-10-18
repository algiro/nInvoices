using Mediator;
using nInvoices.Application.Countries;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Application.TaxPrediction;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;
using nInvoices.Core.ValueObjects;

namespace nInvoices.Application.Features.TaxPrediction.Commands;

/// <summary>Saves where the user pays taxes and how (creating the profile the first time).</summary>
public sealed record UpdateTaxProfileCommand(UpdateTaxProfileDto Profile) : IRequest<TaxProfileDto>;

/// <summary>Records what was paid for a form and period, replacing an earlier record of the same.</summary>
public sealed record RecordTaxPaymentCommand(RecordTaxPaymentDto Payment) : IRequest<TaxPaymentDto>;

/// <summary>Result: false if there is no such payment.</summary>
public sealed record DeleteTaxPaymentCommand(long Id) : IRequest<bool>;

public sealed class UpdateTaxProfileCommandHandler : IRequestHandler<UpdateTaxProfileCommand, TaxProfileDto>
{
    private readonly ITaxPredictionRegistry _registry;
    private readonly IRepository<TaxProfile> _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserCountry? _country;
    private readonly IRepository<UserCountry>? _countries;
    private readonly IAppText _text;

    public UpdateTaxProfileCommandHandler(
        ITaxPredictionRegistry registry,
        IRepository<TaxProfile> profiles,
        IUnitOfWork unitOfWork,
        IAppText text,
        IUserCountry? country = null,
        IRepository<UserCountry>? countries = null)
    {
        _registry = registry;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _country = country;
        _countries = countries;
        _text = text;
    }

    /// <exception cref="DomainException">The country or regime is not offered.</exception>
    /// <exception cref="ComplianceValidationException">A required setting is missing or a choice is not an option.</exception>
    public async ValueTask<TaxProfileDto> Handle(UpdateTaxProfileCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Profile;
        var module = _registry.Find(dto.CountryCode)
            ?? throw new DomainException(_text.Translate("errors.taxes.notAvailable", ("country", dto.CountryCode)));
        var regime = module.Regimes.FirstOrDefault(r => r.Id == dto.RegimeId)
            ?? throw new DomainException(_text.Translate("errors.taxes.unknownRegime", ("regime", dto.RegimeId), ("country", module.DisplayName)));

        // Taxes are paid where the user works from; without a country yet, this one becomes it
        if (_country is not null && _countries is not null)
        {
            var current = await _country.GetAsync(cancellationToken);
            if (current is not null && !string.Equals(current, module.CountryCode, StringComparison.OrdinalIgnoreCase))
                throw new DomainException(_text.Translate("errors.taxes.anotherCountry", ("current", current), ("country", module.DisplayName)));
            if (!await _country.IsChosenAsync(cancellationToken))
                await _countries.AddAsync(new UserCountry(module.CountryCode), cancellationToken);
        }

        // Keep only the values the regime asks for
        var fields = regime.Fields.ToDictionary(f => f.Key);
        var values = (dto.Values ?? new Dictionary<string, string>())
            .Where(v => fields.ContainsKey(v.Key) && !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => v.Value.Trim());

        var issues = Check(regime.Fields, values);
        if (issues.Count > 0)
            throw new ComplianceValidationException(issues);

        // The query filter returns only the current user's rows
        var profile = (await _profiles.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (profile is null)
        {
            profile = new TaxProfile(module.CountryCode, regime.Id, values);
            await _profiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            profile.Update(module.CountryCode, regime.Id, values);
            await _profiles.UpdateAsync(profile, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return TaxPredictionMapper.ToDto(profile);
    }

    private static List<ComplianceIssue> Check(IReadOnlyList<ComplianceField> fields, Dictionary<string, string> values)
    {
        var issues = new List<ComplianceIssue>();
        foreach (var field in fields)
        {
            var value = values.GetValueOrDefault(field.Key);
            if (value is null)
            {
                if (field.Required)
                    issues.Add(new ComplianceIssue(field.Key, $"\"{field.Label}\" is required"));
            }
            else if (field.Type == ComplianceFieldType.Choice && field.Options?.Any(o => o.Value == value) != true)
            {
                issues.Add(new ComplianceIssue(field.Key, $"\"{value}\" is not an option for \"{field.Label}\""));
            }
            else if (field.Type == ComplianceFieldType.Boolean && value != "true")
            {
                issues.Add(new ComplianceIssue(field.Key, $"\"{field.Label}\" is ticked with \"true\" or left out"));
            }
        }
        return issues;
    }
}

public sealed class RecordTaxPaymentCommandHandler : IRequestHandler<RecordTaxPaymentCommand, TaxPaymentDto>
{
    private readonly ITaxPredictionRegistry _registry;
    private readonly IRepository<TaxProfile> _profiles;
    private readonly IRepository<TaxPayment> _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppText _text;

    public RecordTaxPaymentCommandHandler(
        ITaxPredictionRegistry registry,
        IRepository<TaxProfile> profiles,
        IRepository<TaxPayment> payments,
        IUnitOfWork unitOfWork,
        IAppText text)
    {
        _registry = registry;
        _profiles = profiles;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _text = text;
    }

    /// <exception cref="DomainException">The user hasn't said where they pay taxes.</exception>
    public async ValueTask<TaxPaymentDto> Handle(RecordTaxPaymentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Payment;

        // The query filter returns only the current user's rows; payments are in the country's currency
        var profile = (await _profiles.GetAllAsync(cancellationToken)).FirstOrDefault();
        var module = profile is null ? null : _registry.Find(profile.CountryCode);
        if (module is null)
            throw new DomainException(_text.Translate("errors.taxes.chooseCountryFirst"));

        var form = dto.Form.Trim();
        var amount = new Money(dto.Amount, module.Currency);
        var payment = (await _payments.FindAsync(p => p.Year == dto.Year && p.Period == dto.Period && p.Form == form, cancellationToken))
            .FirstOrDefault();

        if (payment is null)
        {
            payment = new TaxPayment(dto.Year, dto.Period, form, amount, dto.PaidOn);
            await _payments.AddAsync(payment, cancellationToken);
        }
        else
        {
            payment.Update(amount, dto.PaidOn);
            await _payments.UpdateAsync(payment, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return TaxPredictionMapper.ToDto(payment);
    }
}

public sealed class DeleteTaxPaymentCommandHandler : IRequestHandler<DeleteTaxPaymentCommand, bool>
{
    private readonly IRepository<TaxPayment> _payments;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTaxPaymentCommandHandler(IRepository<TaxPayment> payments, IUnitOfWork unitOfWork)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<bool> Handle(DeleteTaxPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.Id, cancellationToken);
        if (payment is null)
            return false;

        await _payments.DeleteAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
