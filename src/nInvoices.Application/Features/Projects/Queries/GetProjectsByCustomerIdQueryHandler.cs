using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Projects.Queries;

public sealed class GetProjectsByCustomerIdQueryHandler
    : IRequestHandler<GetProjectsByCustomerIdQuery, IEnumerable<ProjectDto>>
{
    private readonly IRepository<Project> _repository;

    public GetProjectsByCustomerIdQueryHandler(IRepository<Project> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ProjectDto>> Handle(
        GetProjectsByCustomerIdQuery request,
        CancellationToken cancellationToken)
    {
        var projects = await _repository.FindAsync(
            p => p.CustomerId == request.CustomerId,
            cancellationToken);

        if (!request.IncludeInactive)
            projects = projects.Where(p => p.IsActive);

        return projects
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ProjectMapper.ToDto);
    }
}
