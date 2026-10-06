using nInvoices.Core.Configuration;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Notifications;

namespace nInvoices.Api.Infrastructure;

public static class AdminNotificationExtensions
{
    /// <summary>Telegram messages to the administrator; off until <c>Notifications:Telegram*</c> are set.</summary>
    public static IServiceCollection AddAdminNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        // Without an explicit link, the user's page in the Keycloak admin console is derived from the realm the API trusts
        services.PostConfigure<NotificationOptions>(options =>
            options.KeycloakUserUrl ??= KeycloakUserUrlTemplate(configuration));
        services.AddHttpClient<IAdminNotifier, TelegramAdminNotifier>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    /// <summary>
    /// The user's role-mapping page in the Keycloak admin console, with <c>{userId}</c> where the user's
    /// id goes, from <c>Keycloak:ExternalAuthority</c> (or <c>Keycloak:Authority</c>); null without a realm URL.
    /// </summary>
    public static string? KeycloakUserUrlTemplate(IConfiguration configuration)
    {
        var authority = configuration["Keycloak:ExternalAuthority"] ?? configuration["Keycloak:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            return null;
        var marker = authority.LastIndexOf("/realms/", StringComparison.Ordinal);
        if (marker < 0)
            return null;
        var realm = authority[(marker + "/realms/".Length)..].Trim('/');
        return $"{authority[..marker]}/admin/master/console/#/{realm}/users/{{userId}}/role-mapping";
    }
}
