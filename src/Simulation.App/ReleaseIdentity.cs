using System.Reflection;
using System.Text.RegularExpressions;
using Simulation.Core.Configuration;

namespace Simulation.App;

internal static partial class ReleaseIdentity
{
    private static readonly Assembly AppAssembly = typeof(ReleaseIdentity).Assembly;
    private static readonly Version AssemblyVersion = AppAssembly.GetName().Version ?? new Version(0, 0);

    public static string VersionDirectoryName => AssemblyVersion.Build > 0
        ? $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}"
        : $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}";
    public static string DisplayName => $"World Sim {VersionDirectoryName}";
    public static string InformationalVersion =>
        AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        AssemblyVersion.ToString();
    public static string RepositoryCommit => Metadata("RepositoryCommit");
    public static string RepositoryTreeState => Metadata("RepositoryTreeState");

    public static void ValidateSimulationConfig(string configId)
    {
        var match = VersionPrefix().Match(configId ?? string.Empty);
        if (!match.Success)
        {
            throw new ConfigurationException(
                $"Simulation Config id must start with a release version such as {VersionDirectoryName}-.");
        }

        if (!string.Equals(match.Value, VersionDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(
                $"Application {VersionDirectoryName} cannot run Config {configId}. " +
                "Update the application version and Config together.");
        }
    }

    [GeneratedRegex(@"^v\d+\.\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPrefix();

    private static string Metadata(string key) =>
        AppAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?.Value ??
        "unknown";
}
