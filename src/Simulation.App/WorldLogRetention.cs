using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Simulation.App;

public sealed record WorldLogMaintenanceReport(
    IReadOnlyList<string> ArchivedWorlds,
    IReadOnlyList<string> DeletedReleaseVersions);

internal sealed partial class WorldLogRetention
{
    internal const string CompletionFileName = "completion.json";

    private readonly string _baseLogRoot;
    private readonly string _releaseLogRoot;
    private readonly ObservationAppConfig _config;

    public WorldLogRetention(string baseLogRoot, string releaseVersion, ObservationAppConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLogRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseVersion);
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _baseLogRoot = Path.GetFullPath(baseLogRoot);
        _releaseLogRoot = Path.Combine(_baseLogRoot, releaseVersion);
    }

    public string ReleaseLogRoot => _releaseLogRoot;

    public WorldLogMaintenanceReport MaintainPastLogs()
    {
        Directory.CreateDirectory(_baseLogRoot);
        var deletedVersions = DeleteOtherReleaseVersions();
        Directory.CreateDirectory(_releaseLogRoot);

        var archivedWorlds = new List<string>();
        if (_config.ArchiveCompletedWorldLogs)
        {
            foreach (var directory in EnumerateWorldDirectories().Where(IsCompletedWorldDirectory))
            {
                ArchiveWorld(directory);
                archivedWorlds.Add(Path.GetFileName(directory));
            }
        }

        return new WorldLogMaintenanceReport(archivedWorlds, deletedVersions);
    }

    public int NextWorldNumber()
    {
        var maximum = 0;
        if (!Directory.Exists(_releaseLogRoot))
        {
            return 1;
        }

        foreach (var directory in Directory.EnumerateDirectories(_releaseLogRoot))
        {
            maximum = Math.Max(maximum, ParseWorldNumber(Path.GetFileName(directory)));
        }

        foreach (var archive in Directory.EnumerateFiles(_releaseLogRoot, "*.zip", SearchOption.TopDirectoryOnly))
        {
            maximum = Math.Max(maximum, ParseWorldNumber(Path.GetFileNameWithoutExtension(archive)));
        }

        return checked(maximum + 1);
    }

    public void ArchiveCompletedWorld(string directoryPath)
    {
        if (!_config.ArchiveCompletedWorldLogs)
        {
            return;
        }

        if (!IsCompletedWorldDirectory(directoryPath))
        {
            throw new InvalidOperationException(
                $"World cannot be archived before {CompletionFileName} is committed: {directoryPath}");
        }

        ArchiveWorld(directoryPath);
    }

    private IReadOnlyList<string> DeleteOtherReleaseVersions()
    {
        var deleted = new List<string>();
        if (!_config.DeleteOtherReleaseVersionLogs || !Directory.Exists(_baseLogRoot))
        {
            return deleted;
        }

        foreach (var directory in Directory.EnumerateDirectories(_baseLogRoot)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            if (!ReleaseVersionDirectory().IsMatch(name) ||
                string.Equals(name, Path.GetFileName(_releaseLogRoot), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureDirectChild(_baseLogRoot, directory);
            Directory.Delete(directory, true);
            deleted.Add(name);
        }

        return deleted;
    }

    private IReadOnlyList<string> EnumerateWorldDirectories() =>
        Directory.EnumerateDirectories(_releaseLogRoot)
            .Where(path => ParseWorldNumber(Path.GetFileName(path)) > 0)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static bool IsCompletedWorldDirectory(string directoryPath) =>
        File.Exists(Path.Combine(directoryPath, CompletionFileName));

    private int ParseWorldNumber(string name)
    {
        if (!name.StartsWith(_config.WorldDirectoryPrefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var suffix = name[_config.WorldDirectoryPrefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 0;
    }

    private void ArchiveWorld(string directoryPath)
    {
        var fullDirectoryPath = Path.GetFullPath(directoryPath);
        EnsureDirectChild(_releaseLogRoot, fullDirectoryPath);
        var worldId = Path.GetFileName(fullDirectoryPath);
        if (ParseWorldNumber(worldId) <= 0)
        {
            throw new InvalidOperationException($"Not a configured World log directory: {fullDirectoryPath}");
        }

        if (!Directory.Exists(fullDirectoryPath))
        {
            return;
        }

        var files = Directory.EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            return;
        }

        var archivePath = Path.Combine(_releaseLogRoot, worldId + ".zip");
        if (File.Exists(archivePath))
        {
            ValidateArchive(archivePath, fullDirectoryPath, files);
            WriteChecksum(archivePath);
            Directory.Delete(fullDirectoryPath, true);
            return;
        }

        var temporaryPath = archivePath + $".creating-{Guid.NewGuid():N}";
        try
        {
            ZipFile.CreateFromDirectory(
                fullDirectoryPath,
                temporaryPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);
            ValidateArchive(temporaryPath, fullDirectoryPath, files);
            File.Move(temporaryPath, archivePath);
            WriteChecksum(archivePath);
            Directory.Delete(fullDirectoryPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateArchive(
        string archivePath,
        string sourceDirectory,
        IReadOnlyCollection<string> sourceFiles)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .ToDictionary(entry => entry.FullName.Replace('\\', '/'), StringComparer.Ordinal);
        if (entries.Count != sourceFiles.Count)
        {
            throw new InvalidDataException(
                $"Archive entry count mismatch for {archivePath}: expected {sourceFiles.Count}, got {entries.Count}.");
        }

        foreach (var sourcePath in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath).Replace('\\', '/');
            if (!entries.TryGetValue(relativePath, out var entry))
            {
                throw new InvalidDataException($"Archive is missing {relativePath}: {archivePath}");
            }

            if (entry.Length != new FileInfo(sourcePath).Length)
            {
                throw new InvalidDataException($"Archive length mismatch for {relativePath}: {archivePath}");
            }
        }
    }

    private static void WriteChecksum(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        var checksumPath = archivePath + ".sha256";
        var temporaryPath = checksumPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            $"{hash}  {Path.GetFileName(archivePath)}{Environment.NewLine}",
            new UTF8Encoding(false));
        File.Move(temporaryPath, checksumPath, true);
    }

    private static void EnsureDirectChild(string expectedParent, string candidate)
    {
        var parent = Directory.GetParent(Path.GetFullPath(candidate))?.FullName;
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedParent)),
                parent is null ? null : Path.TrimEndingDirectorySeparator(parent),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Log maintenance target escaped its expected parent: {candidate}");
        }
    }

    [GeneratedRegex(@"^v\d+(?:\.\d+)*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseVersionDirectory();
}
