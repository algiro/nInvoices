namespace nInvoices.Core.Exceptions;

/// <summary>
/// What an exception thrown in Core says to the user: the key of one of the API's texts
/// (<c>Application/Localization/Messages</c>) and the values of its placeholders. Core takes no services, so it
/// can't translate; the API does when it answers (<c>IAppText.MessageOf</c>). A value that is itself a
/// <see cref="UserText"/> is translated too. The exception's own message stays English, for the logs.
/// </summary>
public sealed class UserText
{
    public UserText(string key, params (string Name, object? Value)[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Args = args;
    }

    public string Key { get; }

    public IReadOnlyList<(string Name, object? Value)> Args { get; }
}

public static class UserTextExtensions
{
    private const string DataKey = "nInvoices.UserText";

    /// <summary>
    /// Attaches what <paramref name="exception"/> says to the user, e.g.
    /// <c>throw new ArgumentException("Customer name cannot be empty", nameof(name)).WithText("errors.input.customerName")</c>.
    /// </summary>
    public static TException WithText<TException>(this TException exception, string key, params (string Name, object? Value)[] args)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        exception.Data[DataKey] = new UserText(key, args);
        return exception;
    }

    /// <summary>What the exception says to the user, if Core attached it (<see cref="WithText{TException}"/>); null otherwise.</summary>
    public static UserText? GetUserText(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Data[DataKey] as UserText;
    }
}
