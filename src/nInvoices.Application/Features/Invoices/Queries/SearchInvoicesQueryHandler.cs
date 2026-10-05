using Mediator;
using Microsoft.Extensions.Logging;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Application.Services;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class SearchInvoicesQueryHandler : IRequestHandler<SearchInvoicesQuery, InvoicePageDto>
{
    public const int MaxPageSize = 200;

    private readonly IInvoiceRepository _repository;
    private readonly IDraftInvoiceSynchronizer _drafts;
    private readonly ILogger<SearchInvoicesQueryHandler> _logger;

    public SearchInvoicesQueryHandler(
        IInvoiceRepository repository,
        IDraftInvoiceSynchronizer drafts,
        ILogger<SearchInvoicesQueryHandler> logger)
    {
        _repository = repository;
        _drafts = drafts;
        _logger = logger;
    }

    public async ValueTask<InvoicePageDto> Handle(SearchInvoicesQuery request, CancellationToken cancellationToken)
    {
        // Drafts show the next number; make sure the list does, whatever last changed it (or
        // when the drafts were created). Never lets a problem here stop the list from loading.
        try
        {
            await _drafts.RefreshDraftsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The draft invoice numbers could not be refreshed");
        }

        var dto = request.Search;
        var pageSize = Math.Clamp(dto.PageSize, 1, MaxPageSize);
        var criteria = new InvoiceSearchCriteria(
            dto.Status,
            dto.CustomerId,
            dto.Type,
            dto.Year,
            dto.Search,
            dto.Sort,
            !string.Equals(dto.Dir, "asc", StringComparison.OrdinalIgnoreCase),
            Math.Max(1, dto.Page),
            pageSize);

        var result = await _repository.SearchAsync(criteria, cancellationToken);

        // A page past the end (e.g. after bulk changes emptied it) falls back to the last page
        var lastPage = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)pageSize));
        if (result.Items.Count == 0 && criteria.Page > lastPage)
        {
            criteria = criteria with { Page = lastPage };
            result = await _repository.SearchAsync(criteria, cancellationToken);
        }

        var counts = await _repository.CountByStatusAsync(criteria, cancellationToken);

        return new InvoicePageDto(
            result.Items.Select(InvoiceMapper.ToListItemDto).ToList(),
            result.TotalCount,
            criteria.Page,
            pageSize,
            counts);
    }
}
