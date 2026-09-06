using MediatR;
using nInvoices.Application.DTOs;
using nInvoices.Application.Mappings;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;

namespace nInvoices.Application.Features.Projects.Commands;

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProjectCommandHandler(
        IRepository<Project> projectRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Project;

        var customer = await _customerRepository.GetByIdAsync(dto.CustomerId, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {dto.CustomerId} not found");

        var name = dto.Name?.Trim() ?? string.Empty;
        var existing = await _projectRepository.FindAsync(
            p => p.CustomerId == dto.CustomerId,
            cancellationToken);
        var match = existing.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            throw new InvalidOperationException(
                $"A project named '{name}' already exists for this customer");

        var project = new Project(dto.CustomerId, name);

        await _projectRepository.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ProjectMapper.ToDto(project);
    }
}
