using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nInvoices.Core.Configuration;
using nInvoices.Infrastructure.Notifications;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

[TestFixture]
public sealed class TelegramAdminNotifierTests
{
    private static CancellationToken Token => TestContext.CurrentContext.CancellationToken;

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent("""{"ok":false,"description":"chat not found"}""") };
        }
    }

    private static (TelegramAdminNotifier Notifier, RecordingHandler Handler) Create(
        HttpStatusCode status, string? token = "123:ABC", string? chat = "42")
    {
        var handler = new RecordingHandler(status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") };
        var options = Options.Create(new NotificationOptions { TelegramBotToken = token, TelegramChatId = chat });
        return (new TelegramAdminNotifier(http, options, NullLogger<TelegramAdminNotifier>.Instance), handler);
    }

    [Test]
    public async Task TrySendAsync_Configured_PostsThePlainTextToTheChat()
    {
        var (notifier, handler) = Create(HttpStatusCode.OK);

        (await notifier.TrySendAsync("New account *waiting*", Token)).ShouldBeTrue();

        var (uri, body) = handler.Requests.ShouldHaveSingleItem();
        uri.ToString().ShouldBe("https://api.telegram.org/bot123:ABC/sendMessage");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("chat_id").GetString().ShouldBe("42");
        json.RootElement.GetProperty("text").GetString().ShouldBe("New account *waiting*");
        // No parse_mode: what a user typed as their name can't format the message
        json.RootElement.TryGetProperty("parse_mode", out _).ShouldBeFalse();
    }

    [Test]
    public async Task TrySendAsync_TelegramRefuses_ReturnsFalseWithoutThrowing()
    {
        var (notifier, _) = Create(HttpStatusCode.BadRequest);

        (await notifier.TrySendAsync("hello", Token)).ShouldBeFalse();
    }

    [TestCase(null, "42")]
    [TestCase("123:ABC", "")]
    public async Task TrySendAsync_NotConfigured_SendsNothing(string? token, string? chat)
    {
        var (notifier, handler) = Create(HttpStatusCode.OK, token, chat);

        notifier.IsEnabled.ShouldBeFalse();
        (await notifier.TrySendAsync("hello", Token)).ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
    }
}
