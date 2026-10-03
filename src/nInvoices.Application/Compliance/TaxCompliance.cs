using nInvoices.Core.Compliance;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Compliance;

/// <summary>Stores the country-specific data of a tax, for each regime the user turned on.</summary>
public interface ITaxCompliance
{
    /// <summary>
    /// Sets the values of every regime that applies to the user; those of other countries are left as
    /// they are. A null <paramref name="values"/> changes nothing.
    /// </summary>
    /// <param name="values">Keyed "COUNTRY.field".</param>
    /// <exception cref="ArgumentException">A value is not one the field allows.</exception>
    Task ApplyAsync(Tax tax, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default);
}

public sealed class TaxCompliance : ITaxCompliance
{
    private readonly IComplianceGate _gate;

    public TaxCompliance(IComplianceGate gate)
    {
        _gate = gate;
    }

    public async Task ApplyAsync(Tax tax, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken = default)
    {
        if (values is null)
            return;

        foreach (var active in await _gate.GetActiveAsync(cancellationToken))
        {
            var module = active.Module;
            var prefix = module.CountryCode + ".";
            var fields = module.TaxFields.ToDictionary(f => f.Key);

            var own = new Dictionary<string, string>();
            foreach (var (key, value) in values.Where(v => v.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var name = key[prefix.Length..];
                if (!fields.TryGetValue(name, out var field) || string.IsNullOrWhiteSpace(value))
                    continue;

                if (field.Type == ComplianceFieldType.Choice && field.Options?.Any(o => o.Value == value.Trim()) != true)
                    throw new ArgumentException($"\"{value}\" is not an option for \"{field.Label}\"");

                own[name] = value.Trim();
            }

            tax.SetComplianceValues(module.CountryCode, own);
        }
    }
}
