using System.Security.Cryptography.X509Certificates;

namespace nInvoices.Core.Compliance.EInvoice;

/// <summary>The file to deliver, with what a channel may need to know about who sends it.</summary>
public sealed record EInvoiceShipment(string InvoiceNumber, string FileName, byte[] Content, IssuerProfile Issuer);

/// <summary>Where a delivered e-invoice stands, as the channel reports it.</summary>
/// <param name="Reference">The channel's own identifier of the delivery (a registry code).</param>
/// <param name="StatusCode">The channel's status code.</param>
/// <param name="StatusName">The status in words.</param>
/// <param name="CancellationStatus">Where a cancellation request stands, if the channel has such a thing.</param>
/// <param name="RegisteredAt">When the channel registered the invoice.</param>
public sealed record ChannelDelivery(
    string Reference,
    string? StatusCode,
    string? StatusName,
    string? CancellationStatus,
    DateTime? RegisteredAt);

/// <summary>The channel refused or could not be reached; the message is for the user.</summary>
public sealed class ChannelException : Exception
{
    public ChannelException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Somewhere a structured e-invoice is delivered to (a government platform, a network): Spain's FACe for
/// Facturae, and in time another country's equivalent. A channel belongs to a country and takes the file of
/// one format; whether it is offered on a server, and for which customers, is up to the channel.
/// </summary>
public interface IEInvoiceChannel
{
    /// <summary>Stable identifier, e.g. "face".</summary>
    string ChannelId { get; }

    /// <summary>The country (ISO alpha-2) whose module this channel belongs to.</summary>
    string CountryCode { get; }

    /// <summary>The <see cref="IEInvoiceFormat.FormatId"/> of the files it takes.</summary>
    string FormatId { get; }

    string DisplayName { get; }

    /// <summary>"Test" or "Production": where this server delivers to.</summary>
    string EnvironmentName { get; }

    /// <summary>Why this server does not offer the channel (not set up); null when it does.</summary>
    string? UnavailableReason { get; }

    /// <summary>Whether invoices to this customer are delivered through the channel.</summary>
    bool AppliesTo(EInvoiceParty buyer);

    /// <summary>What the issuer still has to fill in before delivering; empty when nothing.</summary>
    IReadOnlyList<ComplianceIssue> CheckReady(IssuerProfile issuer);

    /// <exception cref="ChannelException">The channel refused the file or could not be reached.</exception>
    Task<ChannelDelivery> SendAsync(EInvoiceShipment shipment, X509Certificate2 certificate, CancellationToken cancellationToken = default);

    /// <summary>Asks the channel where a delivered invoice stands now.</summary>
    /// <exception cref="ChannelException">The channel could not be asked.</exception>
    Task<ChannelDelivery> RefreshAsync(string reference, X509Certificate2 certificate, CancellationToken cancellationToken = default);
}
