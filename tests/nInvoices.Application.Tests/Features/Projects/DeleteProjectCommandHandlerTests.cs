using System.Linq.Expressions;
using Moq;
using nInvoices.Application.Features.Projects.Commands;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Features.Projects;

[TestFixture]
public sealed class DeleteProjectCommandHandlerTests
{
    private Mock<IRepository<Project>> _projectRepository = null!;
    private Mock<IRepository<WorkDayProject>> _workDayProjectRepository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private DeleteProjectCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _projectRepository = new Mock<IRepository<Project>>();
        _workDayProjectRepository = new Mock<IRepository<WorkDayProject>>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _handler = new DeleteProjectCommandHandler(
            _projectRepository.Object,
            _workDayProjectRepository.Object,
            _unitOfWork.Object);
    }

    [Test]
    public async Task Handle_WhenProjectMissing_ReturnsNotFound()
    {
        _projectRepository
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await _handler.Handle(new DeleteProjectCommand(5), TestContext.CurrentContext.CancellationToken);

        result.Found.ShouldBeFalse();
        result.Deleted.ShouldBeFalse();
        result.Deactivated.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_WhenProjectUnreferenced_HardDeletes()
    {
        var project = new Project(1, "Alpha") { Id = 5 };
        _projectRepository
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _workDayProjectRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<WorkDayProject, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new DeleteProjectCommand(5), TestContext.CurrentContext.CancellationToken);

        result.Deleted.ShouldBeTrue();
        result.Deactivated.ShouldBeFalse();
        _projectRepository.Verify(r => r.DeleteAsync(project, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_WhenProjectReferencedByWorkDay_SoftDeletes()
    {
        var project = new Project(1, "Alpha") { Id = 5 };
        _projectRepository
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _workDayProjectRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<WorkDayProject, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkDayProject(5L, 4m)]);

        var result = await _handler.Handle(new DeleteProjectCommand(5), TestContext.CurrentContext.CancellationToken);

        result.Deleted.ShouldBeFalse();
        result.Deactivated.ShouldBeTrue();
        project.IsActive.ShouldBeFalse();
        _projectRepository.Verify(r => r.DeleteAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
        _projectRepository.Verify(r => r.UpdateAsync(project, It.IsAny<CancellationToken>()), Times.Once);
    }
}
