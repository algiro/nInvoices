using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Projects.Commands;

public sealed class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    private readonly IRepository<Project> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProjectCommandHandler(IRepository<Project> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectDto> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (project == null)
            throw new KeyNotFoundException($"Project with ID {request.Id} not found");

        var dto = request.Project;
        var name = dto.Name?.Trim() ?? string.Empty;

        if (!string.Equals(name, project.Name, StringComparison.OrdinalIgnoreCase))
        {
            var siblings = await _repository.FindAsync(
                p => p.CustomerId == project.CustomerId,
                cancellationToken);
            var clash = siblings.FirstOrDefault(p =>
                p.Id != project.Id &&
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (clash is not null)
                throw new InvalidOperationException(
                    $"A project named '{name}' already exists for this customer");
        }

        project.Rename(name);

        if (dto.IsActive)
            project.Activate();
        else
            project.Deactivate();

        await _repository.UpdateAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ProjectMapper.ToDto(project);
    }
}
