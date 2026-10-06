using FluentValidation;
using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.ImportExport;

public sealed record ExportCustomersQuery : IRequest<DataExportDto>;

public sealed record ExportInvoicesQuery(int? Year, int? Month, long? CustomerId) : IRequest<DataExportDto>;

public sealed record ExportSettingsQuery : IRequest<DataExportDto>;

public sealed record ImportCustomersCommand(DataExportDto Data) : IRequest<ImportResultDto>;

public sealed record ImportInvoicesCommand(DataExportDto Data) : IRequest<ImportResultDto>;

public sealed record ImportSettingsCommand(DataExportDto Data) : IRequest<ImportResultDto>;

public sealed class ImportExportHandlers :
    IRequestHandler<ExportCustomersQuery, DataExportDto>,
    IRequestHandler<ExportInvoicesQuery, DataExportDto>,
    IRequestHandler<ExportSettingsQuery, DataExportDto>,
    IRequestHandler<ImportCustomersCommand, ImportResultDto>,
    IRequestHandler<ImportInvoicesCommand, ImportResultDto>,
    IRequestHandler<ImportSettingsCommand, ImportResultDto>
{
    private readonly IDataPortability _data;

    public ImportExportHandlers(IDataPortability data)
    {
        _data = data;
    }

    public async ValueTask<DataExportDto> Handle(ExportCustomersQuery request, CancellationToken cancellationToken) =>
        await _data.ExportCustomersAsync(cancellationToken);

    public async ValueTask<DataExportDto> Handle(ExportInvoicesQuery request, CancellationToken cancellationToken) =>
        await _data.ExportInvoicesAsync(request.Year, request.Month, request.CustomerId, cancellationToken);

    public async ValueTask<DataExportDto> Handle(ExportSettingsQuery request, CancellationToken cancellationToken) =>
        await _data.ExportSettingsAsync(cancellationToken);

    public async ValueTask<ImportResultDto> Handle(ImportCustomersCommand request, CancellationToken cancellationToken) =>
        await _data.ImportCustomersAsync(request.Data, cancellationToken);

    public async ValueTask<ImportResultDto> Handle(ImportInvoicesCommand request, CancellationToken cancellationToken) =>
        await _data.ImportInvoicesAsync(request.Data, cancellationToken);

    public async ValueTask<ImportResultDto> Handle(ImportSettingsCommand request, CancellationToken cancellationToken) =>
        await _data.ImportSettingsAsync(request.Data, cancellationToken);
}

public sealed class ImportCustomersCommandValidator : AbstractValidator<ImportCustomersCommand>
{
    public ImportCustomersCommandValidator()
    {
        RuleFor(x => x.Data)
            .Must(d => d is { Customers.Count: > 0 } || HasSharedTemplates(d?.SharedTemplates))
            .WithMessage("No customers to import");
    }

    private static bool HasSharedTemplates(SharedTemplatesExportDto? shared) =>
        shared is not null
        && (shared.InvoiceTemplates.Count > 0 || shared.MonthlyReportTemplates.Count > 0 || shared.EmailTemplates.Count > 0);
}

public sealed class ImportInvoicesCommandValidator : AbstractValidator<ImportInvoicesCommand>
{
    public ImportInvoicesCommandValidator()
    {
        RuleFor(x => x.Data)
            .Must(d => d is { Invoices.Count: > 0 })
            .WithMessage("No invoices to import");
    }
}

public sealed class ImportSettingsCommandValidator : AbstractValidator<ImportSettingsCommand>
{
    public ImportSettingsCommandValidator()
    {
        RuleFor(x => x.Data)
            .Must(d => d?.Settings is not null)
            .WithMessage("No settings to import");
    }
}
