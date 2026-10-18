using nInvoices.Application.Localization;

namespace nInvoices.Application.Tests.TestDoubles;

/// <summary>
/// The API's texts as shipped (<c>Localization/Messages</c>), for the classes under test that take an
/// <see cref="IAppText"/>. Tests run in the machine's UI language, English when it has no file.
/// </summary>
public static class TestText
{
    public static readonly IAppText Instance = new AppText();
}
