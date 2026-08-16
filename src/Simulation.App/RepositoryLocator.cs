namespace Simulation.App;

public static class RepositoryLocator
{
    public static string Find(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var resolved = Path.GetFullPath(explicitRoot);
            if (!IsRepositoryRoot(resolved))
            {
                throw new DirectoryNotFoundException(
                    $"Repository root does not contain AGENTS.md and simulation/configs: {resolved}");
            }

            return resolved;
        }

        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                if (IsRepositoryRoot(current.FullName))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        var applicationDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        if (HasSimulationConfigs(applicationDirectory))
        {
            return applicationDirectory;
        }

        throw new DirectoryNotFoundException(
            "urutora repository root or bundled simulation/configs was not found. " +
            "Keep the published folder together or pass --repository-root.");
    }

    private static bool IsRepositoryRoot(string path) =>
        File.Exists(Path.Combine(path, "AGENTS.md")) &&
        HasSimulationConfigs(path);

    private static bool HasSimulationConfigs(string path) =>
        File.Exists(Path.Combine(path, "simulation", "configs", "v0-default.json")) &&
        File.Exists(Path.Combine(path, "simulation", "configs", "observation-app.json"));
}
