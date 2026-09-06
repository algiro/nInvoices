using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Commands;

/// <summary>
/// Command to rename or (de)activate an existing project.
/// </summary>
public sealed record UpdateProjectCommand(long Id, UpdateProjectDto Project) : IRequest<ProjectDto>;
