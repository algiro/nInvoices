namespace nInvoices.Application.Exceptions;

/// <summary>
/// The resource a request is about (or the customer it belongs to) does not exist, or belongs to
/// another user (404). Derives from <see cref="KeyNotFoundException"/>, so code catching that still
/// catches it.
/// </summary>
public sealed class NotFoundException : KeyNotFoundException
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public static NotFoundException For(string resource, long id) => new($"{resource} {id} not found");
}
