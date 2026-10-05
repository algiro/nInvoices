using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;
using nInvoices.Core.Interfaces;

namespace nInvoices.Infrastructure.Notifications;

/// <summary>Sends administrator messages through a Telegram bot (plain text, so user input can't format them).</summary>
public sealed class TelegramAdminNotifier : IAdminNotifier
{
    private readonly HttpClient _http;
    private readonly NotificationOptions _options;
    private readonly ILogger<TelegramAdminNotifier> _logger;

    public TelegramAdminNotifier(HttpClient http, IOptions<NotificationOptions> options, ILogger<TelegramAdminNotifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.TelegramEnabled;

    public async Task<bool> TrySendAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!IsEnabled)
            return false;

        try
        {
            // "./": a token has a colon ("123:ABC"), which would make "bot123:ABC/..." an absolute URI
            using var response = await _http.PostAsJsonAsync(
                $"./bot{_options.TelegramBotToken}/sendMessage",
                new { chat_id = _options.TelegramChatId, text, disable_web_page_preview = true },
                cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            // The body says why (wrong chat id, bot blocked...); the URL holds the token, so it isn't logged
            _logger.LogWarning("Telegram refused the administrator message: {Status} {Body}",
                (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Could not reach Telegram to send the administrator message: {Error}", ex.Message);
            return false;
        }
    }
}
