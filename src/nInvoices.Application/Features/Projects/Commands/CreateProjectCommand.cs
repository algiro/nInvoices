using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Commands;

/// <summary>
/// Command to create a new project for a customer.
/// </summary>
public sealed record CreateProjectCommand(CreateProjectDto Project) : IRequest<ProjectDto>;
