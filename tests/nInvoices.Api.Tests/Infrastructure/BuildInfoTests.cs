using nInvoices.Api.Infrastructure;
using Shouldly;

namespace nInvoices.Api.Tests.Infrastructure;

[TestFixture]
public sealed class BuildInfoTests
{
    [TestCase("1.0.0+a1b2c3d", "1.0.0", "a1b2c3d")]
    [TestCase("1.0.0+a1b2c3d-dirty", "1.0.0", "a1b2c3d-dirty")]
    [TestCase("1.0.0", "1.0.0", null)]
    [TestCase("1.0.0+", "1.0.0", null)]
    [TestCase("", "unknown", null)]
    [TestCase(null, "unknown", null)]
    public void Parse_InformationalVersion_SplitsReleaseAndCommit(string? input, string version, string? commit)
    {
        var result = BuildInfo.Parse(input);

        result.Version.ShouldBe(version);
        result.Commit.ShouldBe(commit);
    }
}
