using MediatR;
using nInvoices.Application.DTOs;

namespace nInvoices.Application.Features.Projects.Queries;

/// <summary>
/// Query to get projects for a specific customer. By default inactive projects are
/// included so the admin screen can manage them; pass <c>false</c> for suggestion lists.
/// </summary>
public sealed record GetProjectsByCustomerIdQuery(long CustomerId, bool IncludeInactive = true)
    : IRequest<IEnumerable<ProjectDto>>;
