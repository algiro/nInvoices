using System.Reflection;
using nInvoices.Application.DTOs;

namespace nInvoices.Api.Infrastructure;

/// <summary>Reads the release number and commit stamped into the assembly at build time (<c>1.0.0+a1b2c3d</c>).</summary>
public static class BuildInfo
{
    public static AppVersionDto Current { get; } = Parse(
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static AppVersionDto Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new AppVersionDto("unknown", null);
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return new AppVersionDto(informationalVersion, null);
        }

        var commit = informationalVersion[(plus + 1)..];
        return new AppVersionDto(informationalVersion[..plus], commit.Length == 0 ? null : commit);
    }
}
