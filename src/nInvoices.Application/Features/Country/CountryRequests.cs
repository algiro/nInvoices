using FluentValidation;
using Mediator;
using nInvoices.Application.Compliance;
using nInvoices.Application.Countries;
using nInvoices.Application.DTOs;
using nInvoices.Application.TaxPrediction;
using nInvoices.Core.Entities;
using nInvoices.Core.Exceptions;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Country;

/// <summary>The country the user works from, and the countries offered.</summary>
public sealed record GetUserCountryQuery : IRequest<UserCountryDto>;

/// <summary>
/// Sets the country the user works from. The invoicing rules of every other country are turned off (their
/// settings are kept); a tax profile of another country no longer applies until the user sets one here.
/// </summary>
public sealed record SetUserCountryCommand(SetUserCountryDto Country) : IRequest<UserCountryDto>;

public sealed class SetUserCountryCommandValidator : AbstractValidator<SetUserCountryCommand>
{
    public SetUserCountryCommandValidator()
    {
        RuleFor(x => x.Country).NotNull();
        RuleFor(x => x.Country.CountryCode)
            .NotEmpty().WithMessage("Choose a country")
            .Length(2).WithMessage("The country must be a two-letter ISO code");
    }
}

/// <summary>The countries offered: those with invoicing rules or tax estimates, by name.</summary>
internal static class OfferedCountries
{
    public static IReadOnlyList<OfferedCountryDto> List(IComplianceRegistry compliance, ITaxPredictionRegistry taxes) =>
        compliance.Offered.Select(m => (m.CountryCode, m.DisplayName))
            .Concat(taxes.Offered.Select(m => (m.CountryCode, m.DisplayName)))
            .GroupBy(c => c.CountryCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OfferedCountryDto(
                g.Key,
                g.First().DisplayName,
                compliance.Find(g.Key) is not null,
                taxes.Find(g.Key) is not null))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}

public sealed class GetUserCountryQueryHandler : IRequestHandler<GetUserCountryQuery, UserCountryDto>
{
    private readonly IUserCountry _country;
    private readonly IComplianceRegistry _compliance;
    private readonly ITaxPredictionRegistry _taxes;

    public GetUserCountryQueryHandler(IUserCountry country, IComplianceRegistry compliance, ITaxPredictionRegistry taxes)
    {
        _country = country;
        _compliance = compliance;
        _taxes = taxes;
    }

    public async ValueTask<UserCountryDto> Handle(GetUserCountryQuery request, CancellationToken cancellationToken) => new(
        await _country.GetAsync(cancellationToken),
        await _country.IsChosenAsync(cancellationToken),
        OfferedCountries.List(_compliance, _taxes));
}

public sealed class SetUserCountryCommandHandler : IRequestHandler<SetUserCountryCommand, UserCountryDto>
{
    private readonly IRepository<UserCountry> _countries;
    private readonly IRepository<ComplianceSettings> _compliance;
    private readonly IComplianceRegistry _complianceRegistry;
    private readonly ITaxPredictionRegistry _taxes;
    private readonly IUnitOfWork _unitOfWork;

    public SetUserCountryCommandHandler(
        IRepository<UserCountry> countries,
        IRepository<ComplianceSettings> compliance,
        IComplianceRegistry complianceRegistry,
        ITaxPredictionRegistry taxes,
        IUnitOfWork unitOfWork)
    {
        _countries = countries;
        _compliance = compliance;
        _complianceRegistry = complianceRegistry;
        _taxes = taxes;
        _unitOfWork = unitOfWork;
    }

    /// <exception cref="DomainException">The country is not offered here.</exception>
    public async ValueTask<UserCountryDto> Handle(SetUserCountryCommand request, CancellationToken cancellationToken)
    {
        var offered = OfferedCountries.List(_complianceRegistry, _taxes);
        var country = offered.FirstOrDefault(c => string.Equals(c.CountryCode, request.Country.CountryCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException($"{request.Country.CountryCode} is not one of the countries supported here");

        // The query filters return only the current user's rows
        var current = (await _countries.GetAllAsync(cancellationToken)).FirstOrDefault();
        if (current is null)
        {
            await _countries.AddAsync(new UserCountry(country.CountryCode), cancellationToken);
        }
        else
        {
            current.Change(country.CountryCode);
            await _countries.UpdateAsync(current, cancellationToken);
        }

        // One country at a time: the others' rules are turned off, their settings kept for a way back
        foreach (var other in (await _compliance.GetAllAsync(cancellationToken))
                     .Where(s => s.IsEnabled && !string.Equals(s.CountryCode, country.CountryCode, StringComparison.OrdinalIgnoreCase)))
        {
            other.TurnOff();
            await _compliance.UpdateAsync(other, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new UserCountryDto(country.CountryCode, true, offered);
    }
}
