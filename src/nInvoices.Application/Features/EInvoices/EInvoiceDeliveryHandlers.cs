using MediatR;
using nInvoices.Application.Compliance.EInvoice;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;

namespace nInvoices.Application.Features.EInvoices;

/// <summary>The delivery channels (FACe...) that take this invoice, with what is missing to use them.</summary>
public sealed record GetInvoiceChannelsQuery(long InvoiceId) : IRequest<IReadOnlyList<EInvoiceChannelDto>>;

/// <summary>Sends the e-invoice of an invoice through a channel. Always the user's own act.</summary>
public sealed record SendInvoiceEInvoiceCommand(long InvoiceId, string ChannelId) : IRequest<EInvoiceDeliveryDto>;

/// <summary>Asks the channel where a sent e-invoice stands.</summary>
public sealed record RefreshInvoiceEInvoiceCommand(long InvoiceId, string ChannelId) : IRequest<EInvoiceDeliveryDto>;

public static class EInvoiceDeliveryMapper
{
    public static EInvoiceSubmissionDto ToDto(EInvoiceSubmission s) => new(
        s.Reference, s.Environment, s.SubmittedAt, s.RegisteredAt, s.StatusCode, s.StatusName, s.CancellationStatus, s.CheckedAt, s.LastError);

    public static EInvoiceDeliveryDto ToDto(DeliveryResult result) =>
        new(result.Succeeded, result.Message, result.Submission is null ? null : ToDto(result.Submission));

    public static EInvoiceChannelDto ToDto(ChannelStatus status)
    {
        var problems = new List<string>();
        if (status.Submission is null)
        {
            if (status.Channel.UnavailableReason is { } unavailable)
                problems.Add(unavailable);
            if (!status.HasFile)
                problems.Add("Generate the e-invoice first");
            problems.AddRange(status.NotReady.Select(i => i.Message));
        }

        return new EInvoiceChannelDto(
            status.Channel.ChannelId, status.Channel.DisplayName, status.Channel.CountryCode, status.Channel.FormatId,
            status.Channel.EnvironmentName,
            status.Submission is null && problems.Count == 0,
            problems,
            status.Submission is null ? null : ToDto(status.Submission));
    }
}

public sealed class GetInvoiceChannelsQueryHandler : IRequestHandler<GetInvoiceChannelsQuery, IReadOnlyList<EInvoiceChannelDto>>
{
    private readonly IEInvoiceDeliveryService _delivery;

    public GetInvoiceChannelsQueryHandler(IEInvoiceDeliveryService delivery)
    {
        _delivery = delivery;
    }

    /// <exception cref="KeyNotFoundException">The invoice does not exist.</exception>
    public async Task<IReadOnlyList<EInvoiceChannelDto>> Handle(GetInvoiceChannelsQuery request, CancellationToken cancellationToken) =>
        (await _delivery.GetChannelsAsync(request.InvoiceId, cancellationToken))
            .Where(c => c.Applies || c.Submission is not null)
            .Select(EInvoiceDeliveryMapper.ToDto)
            .ToList();
}

public sealed class SendInvoiceEInvoiceCommandHandler : IRequestHandler<SendInvoiceEInvoiceCommand, EInvoiceDeliveryDto>
{
    private readonly IEInvoiceDeliveryService _delivery;

    public SendInvoiceEInvoiceCommandHandler(IEInvoiceDeliveryService delivery)
    {
        _delivery = delivery;
    }

    public async Task<EInvoiceDeliveryDto> Handle(SendInvoiceEInvoiceCommand request, CancellationToken cancellationToken) =>
        EInvoiceDeliveryMapper.ToDto(await _delivery.SendAsync(request.InvoiceId, request.ChannelId, cancellationToken));
}

public sealed class RefreshInvoiceEInvoiceCommandHandler : IRequestHandler<RefreshInvoiceEInvoiceCommand, EInvoiceDeliveryDto>
{
    private readonly IEInvoiceDeliveryService _delivery;

    public RefreshInvoiceEInvoiceCommandHandler(IEInvoiceDeliveryService delivery)
    {
        _delivery = delivery;
    }

    public async Task<EInvoiceDeliveryDto> Handle(RefreshInvoiceEInvoiceCommand request, CancellationToken cancellationToken) =>
        EInvoiceDeliveryMapper.ToDto(await _delivery.RefreshAsync(request.InvoiceId, request.ChannelId, cancellationToken));
}
