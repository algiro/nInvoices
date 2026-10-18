using System.Security.Cryptography;
using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Exceptions;
using nInvoices.Application.Localization;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services.Email;
using nInvoices.Application.Telemetry;
using nInvoices.Core.Entities;
using nInvoices.Core.Enums;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.InvoiceEmails.Commands;

public sealed class CreateInvoiceEmailDraftCommandHandler : IRequestHandler<CreateInvoiceEmailDraftCommand, InvoiceEmailDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<GmailConnection> _connectionRepository;
    private readonly IRepository<InvoiceEmail> _emailRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceEmailComposer _composer;
    private readonly IGmailClient _gmailClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IUserContext _userContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateInvoiceEmailDraftCommandHandler> _logger;
    private readonly IAppText _text;

    public CreateInvoiceEmailDraftCommandHandler(
        IInvoiceRepository invoiceRepository,
        IRepository<Customer> customerRepository,
        IRepository<GmailConnection> connectionRepository,
        IRepository<InvoiceEmail> emailRepository,
        IUnitOfWork unitOfWork,
        IInvoiceEmailComposer composer,
        IGmailClient gmailClient,
        ISecretProtector secretProtector,
        IUserContext userContext,
        TimeProvider timeProvider,
        ILogger<CreateInvoiceEmailDraftCommandHandler> logger,
        IAppText text)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
        _connectionRepository = connectionRepository;
        _emailRepository = emailRepository;
        _unitOfWork = unitOfWork;
        _composer = composer;
        _gmailClient = gmailClient;
        _secretProtector = secretProtector;
        _userContext = userContext;
        _timeProvider = timeProvider;
        _logger = logger;
        _text = text;
    }

    public async ValueTask<InvoiceEmailDto> Handle(CreateInvoiceEmailDraftCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Email;

        var invoice = await _invoiceRepository.GetByIdWithRelatedAsync(request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.invoice", ("id", request.InvoiceId)));

        // A draft invoice can still change and a cancelled one must not be sent
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvoiceEmailException(
                InvoiceEmailException.InvoiceNotReady,
                invoice.Status == InvoiceStatus.Draft
                    ? _text.Translate("errors.email.finalizeFirst")
                    : _text.Translate("errors.email.cancelled"));

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new NotFoundException(_text.Translate("notFound.customer", ("id", invoice.CustomerId)));

        var to = EmailAddresses.Split(dto.To);
        var cc = EmailAddresses.Split(dto.Cc);
        if (to.Count == 0)
            throw new InvoiceEmailException(InvoiceEmailException.InvalidRecipients, _text.Translate("errors.email.noRecipients"));
        var invalid = to.Concat(cc).FirstOrDefault(a => !EmailAddresses.IsValid(a));
        if (invalid is not null)
            throw new InvoiceEmailException(InvoiceEmailException.InvalidRecipients, _text.Translate("errors.email.invalidAddress", ("address", invalid)));

        // Headers are single lines
        var subject = string.Join(' ', (dto.Subject ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (subject.Length == 0 || string.IsNullOrWhiteSpace(dto.Body))
            throw new InvoiceEmailException(InvoiceEmailException.InvalidTemplate, _text.Translate("errors.email.subjectAndBody"));

        if (!_gmailClient.IsConfigured)
            throw new InvoiceEmailException(InvoiceEmailException.GmailNotConfigured, _text.Translate("errors.gmail.notConfigured"));

        var userId = _userContext.RequireUserId();
        var connection = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault()
            ?? throw new InvoiceEmailException(InvoiceEmailException.GmailNotConnected, _text.Translate("errors.gmail.notConnected"));

        string refreshToken;
        try
        {
            refreshToken = _secretProtector.Unprotect(connection.EncryptedRefreshToken);
        }
        catch (CryptographicException ex)
        {
            await ForgetConnectionAsync(connection, ex, cancellationToken);
            throw new InvoiceEmailException(InvoiceEmailException.GmailReconnectRequired, _text.Translate("errors.gmail.unreadable"), ex);
        }

        var attachments = await _composer.BuildAttachmentsAsync(invoice, customer, dto.IncludeMonthlyReport, cancellationToken);
        var messageId = $"<{Guid.NewGuid():N}.invoice-{invoice.Id}@ninvoices>";

        GmailDraft draft;
        try
        {
            draft = await _gmailClient.CreateDraftAsync(
                refreshToken,
                new OutgoingEmail(connection.EmailAddress, to, cc, subject, dto.Body, messageId, attachments),
                cancellationToken);
        }
        catch (GmailAuthorizationException ex)
        {
            await ForgetConnectionAsync(connection, ex, cancellationToken);
            throw new InvoiceEmailException(InvoiceEmailException.GmailReconnectRequired, _text.Translate("errors.gmail.revoked"), ex);
        }

        var email = new InvoiceEmail
        {
            InvoiceId = invoice.Id,
            From = connection.EmailAddress,
            To = string.Join(", ", to),
            Cc = cc.Count > 0 ? string.Join(", ", cc) : null,
            Subject = subject,
            Attachments = string.Join(", ", attachments.Select(a => a.FileName)),
            GmailDraftId = draft.DraftId,
            GmailMessageId = draft.MessageId,
            RfcMessageId = messageId
        };
        await _emailRepository.AddAsync(email, cancellationToken);
        connection.MarkUsed(_timeProvider.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        AppMetrics.EmailDrafts.Add(1, new KeyValuePair<string, object?>("outcome", AppMetrics.DraftOutcome.Created));
        _logger.LogInformation("Gmail draft {DraftId} created for invoice {InvoiceId}", draft.DraftId, invoice.Id);
        return InvoiceEmailMapper.ToDto(email);
    }

    private async Task ForgetConnectionAsync(GmailConnection connection, Exception reason, CancellationToken cancellationToken)
    {
        AppMetrics.EmailDrafts.Add(1, new KeyValuePair<string, object?>("outcome", AppMetrics.DraftOutcome.ReconnectRequired));
        _logger.LogWarning(reason, "Removing unusable Gmail connection for {Email}", connection.EmailAddress);
        await _connectionRepository.DeleteAsync(connection, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
