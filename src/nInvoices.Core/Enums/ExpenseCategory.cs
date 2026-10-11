namespace nInvoices.Core.Enums;

/// <summary>What a business expense is for, to group them; it doesn't change how much is deductible.</summary>
public enum ExpenseCategory
{
    Other,

    /// <summary>The freelancer's own social security contribution (in Spain, the cuota de autónomo).</summary>
    SocialSecurity,

    /// <summary>Accountant, gestor, lawyer, consultants.</summary>
    ProfessionalServices,

    /// <summary>Software, subscriptions, hosting.</summary>
    Software,

    /// <summary>Computers, phones, furniture.</summary>
    Equipment,

    /// <summary>Office or coworking rent, utilities, phone and internet.</summary>
    Office,

    Travel,

    Training,

    Insurance,

    /// <summary>Bank and payment fees.</summary>
    Banking
}
