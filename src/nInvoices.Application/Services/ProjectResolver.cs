using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Services;

/// <summary>
/// Resolves a project for a customer by id or name, creating it on first use.
/// Shared by the projects CRUD and invoice generation so that names typed on the
/// calendar are auto-created and matched case-insensitively.
/// </summary>
public interface IProjectResolver
{
    /// <summary>
    /// Returns the project identified by <paramref name="projectId"/> (validated against the
    /// customer) or matched by trimmed, case-insensitive name. A new active project is created
    /// when nothing matches; an existing inactive match is reactivated.
    /// The returned entity is tracked but not yet persisted.
    /// </summary>
    Task<Project> ResolveOrCreateAsync(
        long customerId,
        long? projectId,
        string name,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectResolver : IProjectResolver
{
    private readonly IRepository<Project> _projectRepository;

    // Tracks projects created during this scope so repeated references to the same
    // new name (e.g. the same project on several days of one invoice) reuse one row
    // rather than colliding on the unique (CustomerId, Name) index.
    private readonly List<Project> _pendingCreations = [];

    public ProjectResolver(IRepository<Project> projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Project> ResolveOrCreateAsync(
        long customerId,
        long? projectId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
            throw new ArgumentException("Customer ID must be positive", nameof(customerId));

        var customerProjects = await _projectRepository.FindAsync(
            p => p.CustomerId == customerId,
            cancellationToken);
        var projects = customerProjects
            .Concat(_pendingCreations.Where(p => p.CustomerId == customerId))
            .ToList();

        if (projectId is > 0)
        {
            var byId = projects.FirstOrDefault(p => p.Id == projectId.Value)
                ?? throw new InvalidOperationException(
                    $"Project {projectId.Value} does not belong to customer {customerId}");

            if (!byId.IsActive)
                byId.Activate();

            return byId;
        }

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));

        var trimmed = name.Trim();
        var byName = projects.FirstOrDefault(p =>
            string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (byName is not null)
        {
            if (!byName.IsActive)
                byName.Activate();

            return byName;
        }

        var created = new Project(customerId, trimmed);
        await _projectRepository.AddAsync(created, cancellationToken);
        _pendingCreations.Add(created);
        return created;
    }
}
