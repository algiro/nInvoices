using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Commands;

/// <summary>
/// Command to delete a project. Projects referenced by existing work days are
/// deactivated (soft delete) instead of being removed.
/// </summary>
public sealed record DeleteProjectCommand(long Id) : IRequest<DeleteProjectResultDto>;
