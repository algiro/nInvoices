using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Projects.Queries;

public sealed class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, IEnumerable<ProjectDto>>
{
    private readonly IRepository<Project> _repository;

    public GetAllProjectsQueryHandler(IRepository<Project> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ProjectDto>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _repository.GetAllAsync(cancellationToken);
        return projects.Select(ProjectMapper.ToDto);
    }
}
