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
        services.AddHttpClient<IAdminNotifier, TelegramAdminNotifier>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
