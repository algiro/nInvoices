using nInvoices.Core.Exceptions;

namespace nInvoices.Application.Localization;

/// <summary>Translates what Core attaches to its exceptions (<see cref="UserText"/>), which it can't translate itself.</summary>
public static class UserTextTranslation
{
    /// <summary>The text in the current UI language; a value that is a <see cref="UserText"/> is translated as well.</summary>
    public static string Translate(this IAppText text, UserText userText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(userText);
        return text.Translate(userText.Key, userText.Args.Select(a => (a.Name, a.Value is UserText inner ? text.Translate(inner) : a.Value)).ToArray());
    }

    /// <summary>What <paramref name="exception"/> says to the user: its <see cref="UserText"/> translated, else its message.</summary>
    public static string MessageOf(this IAppText text, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.GetUserText() is { } userText ? text.Translate(userText) : exception.Message;
    }
}
