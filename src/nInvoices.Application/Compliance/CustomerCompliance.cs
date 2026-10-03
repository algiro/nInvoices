using nInvoices.Application.Features.Compliance.Commands;
using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance;

/// <summary>Stores the country-specific data of a customer, checked by the rules of each regime the user turned on.</summary>
public interface ICustomerCompliance
{
    /// <summary>
    /// Sets the values of every regime that applies to the user; those of other countries are left as
    /// they are. A null <paramref name="values"/> changes nothing.
    /// </summary>
    /// <param name="values">Keyed "COUNTRY.field".</param>
    /// <exception cref="ComplianceValidationException">A regime rejects the values.</exception>
    Task ApplyAsync(Customer customer, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default);
}

public sealed class CustomerCompliance : ICustomerCompliance
{
    private readonly IComplianceGate _gate;

    public CustomerCompliance(IComplianceGate gate)
    {
        _gate = gate;
    }

    public async Task ApplyAsync(Customer customer, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default)
    {
        if (values is null)
            return;

        foreach (var active in await _gate.GetActiveAsync(cancellationToken))
        {
            var module = active.Module;
            var prefix = module.CountryCode + ".";
            var known = module.CustomerFields.Select(f => f.Key).ToHashSet();

            var own = values
                .Where(v => v.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(v => (Key: v.Key[prefix.Length..], v.Value))
                .Where(v => known.Contains(v.Key) && !string.IsNullOrWhiteSpace(v.Value))
                .ToDictionary(v => v.Key, v => v.Value.Trim());

            var issues = module.ValidateCustomer(new CustomerProfile(customer.Name, customer.FiscalId, customer.Address, own));
            if (issues.Count > 0)
                throw new ComplianceValidationException(issues);

            customer.SetComplianceValues(module.CountryCode, own);
        }
    }
}
