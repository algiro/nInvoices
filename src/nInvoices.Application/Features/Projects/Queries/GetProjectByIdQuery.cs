using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Queries;

public sealed record GetProjectByIdQuery(long Id) : IRequest<ProjectDto?>;
