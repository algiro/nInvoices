using Mediator;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Queries;

public sealed record GetAllProjectsQuery : IRequest<IEnumerable<ProjectDto>>;
