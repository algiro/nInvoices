using nInvoices.Core.Entities;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class WorkDayProjectTests
{
    [Test]
    public void Constructor_WithProjectEntity_CopiesIdAndKeepsNavigation()
    {
        var project = new Project(1, "Alpha") { Id = 42 };

        var allocation = new WorkDayProject(project, 3.5m);

        allocation.Project.ShouldBeSameAs(project);
        allocation.ProjectId.ShouldBe(42);
        allocation.Hours.ShouldBe(3.5m);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithNonPositiveHours_Throws(int hours)
    {
        var project = new Project(1, "Alpha") { Id = 1 };

        Should.Throw<ArgumentException>(() => new WorkDayProject(project, hours));
    }

    [Test]
    public void UpdateHours_WithPositiveValue_SetsHoursAndStampsUpdatedAt()
    {
        var allocation = new WorkDayProject(1L, 2m);

        allocation.UpdateHours(6m);

        allocation.Hours.ShouldBe(6m);
        allocation.UpdatedAt.ShouldNotBeNull();
    }
}
