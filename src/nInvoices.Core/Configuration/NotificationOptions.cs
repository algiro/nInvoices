namespace nInvoices.Core.Configuration;

/// <summary>
/// Messages to the server's administrator (today: a new account waiting for approval), sent to a
/// Telegram chat. Off while the bot token or the chat id is empty.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>The bot's token from @BotFather. Another program may use the same bot: sending doesn't conflict.</summary>
    public string? TelegramBotToken { get; init; }

    /// <summary>The chat the bot writes to (the administrator's).</summary>
    public string? TelegramChatId { get; init; }

    /// <summary>
    /// Link to a user in the Keycloak admin console, with <c>{userId}</c> where the user's id goes.
    /// Unset: derived from <c>Keycloak:ExternalAuthority</c> (or <c>Keycloak:Authority</c>).
    /// </summary>
    public string? KeycloakUserUrl { get; init; }

    public bool TelegramEnabled => !string.IsNullOrWhiteSpace(TelegramBotToken) && !string.IsNullOrWhiteSpace(TelegramChatId);
}
