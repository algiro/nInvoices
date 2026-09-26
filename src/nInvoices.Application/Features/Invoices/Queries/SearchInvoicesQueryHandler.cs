using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Invoices.Queries;

public sealed class SearchInvoicesQueryHandler : IRequestHandler<SearchInvoicesQuery, InvoicePageDto>
{
    public const int MaxPageSize = 200;

    private readonly IInvoiceRepository _repository;

    public SearchInvoicesQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<InvoicePageDto> Handle(SearchInvoicesQuery request, CancellationToken cancellationToken)
    {
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
