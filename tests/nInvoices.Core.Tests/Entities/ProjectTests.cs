using nInvoices.Core.Entities;
using Shouldly;

namespace nInvoices.Core.Tests.Entities;

[TestFixture]
public sealed class ProjectTests
{
    [Test]
    public void Constructor_WithValidArguments_TrimsNameAndIsActive()
    {
        var project = new Project(1, "  Website redesign  ");

        project.Name.ShouldBe("Website redesign");
        project.CustomerId.ShouldBe(1);
        project.IsActive.ShouldBeTrue();
        project.CreatedAt.ShouldNotBe(default);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public void Constructor_WithNonPositiveCustomerId_Throws(long customerId)
    {
        Should.Throw<ArgumentException>(() => new Project(customerId, "Alpha"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => new Project(1, name));
    }

    [Test]
    public void Rename_TrimsAndStampsUpdatedAt()
    {
        var project = new Project(1, "Old");

        project.Rename("  New name  ");

        project.Name.ShouldBe("New name");
        project.UpdatedAt.ShouldNotBeNull();
    }

    [Test]
    public void Rename_WithBlankName_Throws()
    {
        var project = new Project(1, "Alpha");

        Should.Throw<ArgumentException>(() => project.Rename("  "));
    }

    [Test]
    public void DeactivateThenActivate_TogglesIsActive()
    {
        var project = new Project(1, "Alpha");

        project.Deactivate();
        project.IsActive.ShouldBeFalse();

        project.Activate();
        project.IsActive.ShouldBeTrue();
    }
}
