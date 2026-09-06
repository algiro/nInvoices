using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Projects.Commands;

public sealed class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, DeleteProjectResultDto>
{
    private readonly IRepository<Project> _repository;
    private readonly IRepository<WorkDayProject> _workDayProjectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProjectCommandHandler(
        IRepository<Project> repository,
        IRepository<WorkDayProject> workDayProjectRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _workDayProjectRepository = workDayProjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteProjectResultDto> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (project == null)
            return new DeleteProjectResultDto(Found: false, Deleted: false, Deactivated: false);

        var references = await _workDayProjectRepository.FindAsync(
            wdp => wdp.ProjectId == request.Id,
            cancellationToken);

        if (references.Any())
        {
            project.Deactivate();
            await _repository.UpdateAsync(project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new DeleteProjectResultDto(Found: true, Deleted: false, Deactivated: true);
        }

        await _repository.DeleteAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeleteProjectResultDto(Found: true, Deleted: true, Deactivated: false);
    }
}
