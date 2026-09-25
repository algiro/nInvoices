using nInvoices.Application.Services.Email;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Features.Customers.Commands;

/// <summary>
/// Validates and stores a customer's email recipients (shared by create and update).
/// </summary>
internal static class CustomerContact
{
    /// <exception cref="ArgumentException">An address is not valid.</exception>
    public static void Apply(Customer customer, string? email, string? ccEmails)
    {
        var to = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (to is not null && !EmailAddresses.IsValid(to))
            throw new ArgumentException($"“{to}” is not a valid email address");

        var invalidCc = EmailAddresses.Split(ccEmails).FirstOrDefault(a => !EmailAddresses.IsValid(a));
        if (invalidCc is not null)
            throw new ArgumentException($"“{invalidCc}” in CC is not a valid email address");

        customer.SetContact(to, EmailAddresses.Normalize(ccEmails));
    }
}
