namespace nInvoices.Application.Services.Email;

/// <summary>
/// Used when a customer has no active email template.
/// </summary>
public static class DefaultEmailTemplate
{
    public const string Subject = "Invoice [[ invoiceNumber ]] - [[ customer.name ]]";

    public const string Body = """
        <p>Dear [[ customer.name ]],</p>
        <p>please find attached invoice <strong>[[ invoiceNumber ]]</strong> dated [[ FormatDate date "dd/MM/yyyy" ]][[ if monthDescription ]] for [[ monthDescription ]] [[ year ]][[ end ]], for a total of <strong>[[ FormatCurrency total currency ]]</strong>.</p>
        [[ if dueDate ]]<p>Payment is due by [[ FormatDate dueDate "dd/MM/yyyy" ]].</p>[[ end ]]
        <p>Kind regards</p>
        """;
}
