using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nInvoices.Application.Services.Email;

namespace nInvoices.Infrastructure.Gmail;

public static class GmailExtensions
{
    /// <summary>
    /// Registers the Gmail client and the secret protector. The host must also call
    /// <c>AddDataProtection()</c> with persisted keys. Without Gmail:ClientId/ClientSecret/RedirectUri
    /// the app still starts and the Gmail features report "not configured".
    /// </summary>
    public static IServiceCollection AddGmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.AddSingleton<IGmailClient, GoogleGmailClient>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
