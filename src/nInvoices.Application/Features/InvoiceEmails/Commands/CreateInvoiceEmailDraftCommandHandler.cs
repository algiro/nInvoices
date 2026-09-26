using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services.Email;
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
    private readonly ILogger<CreateInvoiceEmailDraftCommandHandler> _logger;

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
        ILogger<CreateInvoiceEmailDraftCommandHandler> logger)
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
        _logger = logger;
    }

    public async Task<InvoiceEmailDto> Handle(CreateInvoiceEmailDraftCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Email;

        var invoice = await _invoiceRepository.GetByIdWithRelatedAsync(request.InvoiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found");

        // A draft invoice can still change and a cancelled one must not be sent
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvoiceEmailException(
                InvoiceEmailException.InvoiceNotReady,
                invoice.Status == InvoiceStatus.Draft
                    ? "Finalize the invoice before emailing it."
                    : "A cancelled invoice cannot be emailed.");

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer {invoice.CustomerId} not found");

        var to = EmailAddresses.Split(dto.To);
        var cc = EmailAddresses.Split(dto.Cc);
        if (to.Count == 0)
            throw new InvoiceEmailException(InvoiceEmailException.InvalidRecipients, "Add at least one recipient.");
        var invalid = to.Concat(cc).FirstOrDefault(a => !EmailAddresses.IsValid(a));
        if (invalid is not null)
            throw new InvoiceEmailException(InvoiceEmailException.InvalidRecipients, $"“{invalid}” is not a valid email address.");

        // Headers are single lines
        var subject = string.Join(' ', (dto.Subject ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (subject.Length == 0 || string.IsNullOrWhiteSpace(dto.Body))
            throw new InvoiceEmailException(InvoiceEmailException.InvalidTemplate, "The email needs a subject and a body.");

        if (!_gmailClient.IsConfigured)
            throw new InvoiceEmailException(InvoiceEmailException.GmailNotConfigured, "Gmail is not configured on the server.");

        var userId = _userContext.RequireUserId();
        var connection = (await _connectionRepository.FindAsync(c => c.UserId == userId, cancellationToken)).FirstOrDefault()
            ?? throw new InvoiceEmailException(InvoiceEmailException.GmailNotConnected, "Connect your Gmail account in Settings first.");

        string refreshToken;
        try
        {
            refreshToken = _secretProtector.Unprotect(connection.EncryptedRefreshToken);
        }
        catch (CryptographicException ex)
        {
            await ForgetConnectionAsync(connection, ex, cancellationToken);
            throw new InvoiceEmailException(InvoiceEmailException.GmailReconnectRequired, "The Gmail authorization can no longer be read. Connect Gmail again.", ex);
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
            throw new InvoiceEmailException(InvoiceEmailException.GmailReconnectRequired, "Gmail access was revoked or has expired. Connect Gmail again.", ex);
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
        connection.MarkUsed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Gmail draft {DraftId} created for invoice {InvoiceId}", draft.DraftId, invoice.Id);
        return InvoiceEmailMapper.ToDto(email);
    }

    private async Task ForgetConnectionAsync(GmailConnection connection, Exception reason, CancellationToken cancellationToken)
    {
        _logger.LogWarning(reason, "Removing unusable Gmail connection for {Email}", connection.EmailAddress);
        await _connectionRepository.DeleteAsync(connection, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
