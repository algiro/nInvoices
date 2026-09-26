using System.Linq.Expressions;
using Moq;
using nInvoices.Application.Services;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using Shouldly;

namespace nInvoices.Application.Tests.Services;

[TestFixture]
public sealed class ProjectResolverTests
{
    private Mock<IRepository<Project>> _projectRepository = null!;
    private ProjectResolver _resolver = null!;
    private List<Project> _projects = null!;

    [SetUp]
    public void SetUp()
    {
        _projects = [];
        _projectRepository = new Mock<IRepository<Project>>();

        _projectRepository
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Project, bool>> predicate, CancellationToken _) =>
                _projects.Where(predicate.Compile()).ToList());

        _projectRepository
            .Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => { _projects.Add(p); return p; });

        _resolver = new ProjectResolver(_projectRepository.Object);
    }

    [Test]
    public async Task ResolveOrCreateAsync_WithUnknownName_CreatesActiveProject()
    {
        var result = await _resolver.ResolveOrCreateAsync(1, null, "  New Project  ", TestContext.CurrentContext.CancellationToken);

        result.Name.ShouldBe("New Project");
        result.CustomerId.ShouldBe(1);
        result.IsActive.ShouldBeTrue();
        _projectRepository.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResolveOrCreateAsync_WithNameMatchingExistingCaseInsensitively_ReusesIt()
    {
        _projects.Add(new Project(1, "Alpha") { Id = 7 });

        var result = await _resolver.ResolveOrCreateAsync(1, null, "alpha", TestContext.CurrentContext.CancellationToken);

        result.Id.ShouldBe(7);
        _projectRepository.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ResolveOrCreateAsync_WithInactiveMatch_ReactivatesIt()
    {
        var inactive = new Project(1, "Alpha") { Id = 7 };
        inactive.Deactivate();
        _projects.Add(inactive);

        var result = await _resolver.ResolveOrCreateAsync(1, null, "Alpha", TestContext.CurrentContext.CancellationToken);

        result.Id.ShouldBe(7);
        result.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task ResolveOrCreateAsync_WithProjectIdForAnotherCustomer_Throws()
    {
        _projects.Add(new Project(2, "Alpha") { Id = 7 });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _resolver.ResolveOrCreateAsync(1, 7, "Alpha", TestContext.CurrentContext.CancellationToken));
    }

    [Test]
    public async Task ResolveOrCreateAsync_SameNewNameTwice_CreatesOnlyOneProject()
    {
        var ct = TestContext.CurrentContext.CancellationToken;

        var first = await _resolver.ResolveOrCreateAsync(1, null, "Gamma", ct);
        var second = await _resolver.ResolveOrCreateAsync(1, null, "gamma", ct);

        second.ShouldBeSameAs(first);
        _projectRepository.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ResolveOrCreateAsync_WithProjectId_ReturnsThatProject()
    {
        _projects.Add(new Project(1, "Alpha") { Id = 7 });
        _projects.Add(new Project(1, "Beta") { Id = 8 });

        var result = await _resolver.ResolveOrCreateAsync(1, 8, "ignored", TestContext.CurrentContext.CancellationToken);

        result.Id.ShouldBe(8);
    }
}
