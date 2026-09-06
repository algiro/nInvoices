using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using nInvoices.Application.DTOs;
using nInvoices.Application.Features.Projects.Commands;
using nInvoices.Application.Features.Projects.Queries;

namespace nInvoices.Api.Controllers;

/// <summary>
/// API controller for project management.
/// Projects are scoped per customer and used to break down worked time.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IMediator mediator, ILogger<ProjectsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all projects.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetAll(CancellationToken cancellationToken)
    {
        var projects = await _mediator.Send(new GetAllProjectsQuery(), cancellationToken);
        return Ok(projects);
    }

    /// <summary>
    /// Gets a project by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var project = await _mediator.Send(new GetProjectByIdQuery(id), cancellationToken);

        if (project == null)
        {
            _logger.LogWarning("Project with ID {ProjectId} not found", id);
            return NotFound();
        }

        return Ok(project);
    }

    /// <summary>
    /// Gets all projects for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetByCustomerId(
        long customerId,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var projects = await _mediator.Send(
            new GetProjectsByCustomerIdQuery(customerId, includeInactive),
            cancellationToken);
        return Ok(projects);
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Create(
        [FromBody] CreateProjectDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _mediator.Send(new CreateProjectCommand(dto), cancellationToken);
            _logger.LogInformation("Created project {ProjectId} for customer {CustomerId}",
                project.Id, project.CustomerId);
            return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Customer not found when creating project");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing project (rename / activate / deactivate).
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDto>> Update(
        long id,
        [FromBody] UpdateProjectDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _mediator.Send(new UpdateProjectCommand(id, dto), cancellationToken);
            _logger.LogInformation("Updated project {ProjectId}", id);
            return Ok(project);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Project with ID {ProjectId} not found for update", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a project. If it is referenced by any work day it is deactivated instead.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(DeleteProjectResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteProjectResultDto>> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteProjectCommand(id), cancellationToken);

        if (!result.Found)
        {
            _logger.LogWarning("Project with ID {ProjectId} not found for deletion", id);
            return NotFound();
        }

        _logger.LogInformation(
            "Project {ProjectId} delete request: deleted={Deleted}, deactivated={Deactivated}",
            id, result.Deleted, result.Deactivated);
        return Ok(result);
    }
}
