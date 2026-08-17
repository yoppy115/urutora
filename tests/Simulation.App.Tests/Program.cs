using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using Simulation.App;
using Simulation.Core;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.App.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        ("Observation App configuration schema is strict", ObservationAppConfigurationIsStrict),
        ("world sessions are numbered and logged", WorldSessionsAreNumberedAndLogged),
        ("world logging does not change Simulation events", LoggingDoesNotChangeSimulationEvents),
        ("statistics chart renders changing series", StatisticsChartRendersChangingSeries)
    };

    [STAThread]
    private static int Main()
    {
        var failures = new List<string>();
        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures.Add(name);
                Console.WriteLine($"FAIL {name}");
                Console.WriteLine(exception);
            }
        }

        Console.WriteLine($"APP_TEST_SUMMARY total={Tests.Length} passed={Tests.Length - failures.Count} failed={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void ObservationAppConfigurationIsStrict()
    {
        var config = ObservationAppConfigLoader.Load(AppConfigPath());
        Equal(3, config.SchemaVersion);
        Equal(1, config.SeedIncrement);
        Equal(500, config.NpcActionHistoryDisplayLimit);
        Equal(0.5, config.AgeDistributionBinYears);
        True(config.ArchiveCompletedWorldLogs);
        True(config.DeleteOtherReleaseVersionLogs);
        var invalidPath = Path.Combine(Path.GetTempPath(), $"world-sim-app-invalid-{Guid.NewGuid():N}.json");
        try
        {
            var json = File.ReadAllText(AppConfigPath())
                .Replace("\"schemaVersion\": 3", "\"schemaVersion\": 3, \"unknown\": true", StringComparison.Ordinal);
            File.WriteAllText(invalidPath, json);
            Throws<ConfigurationException>(() => ObservationAppConfigLoader.Load(invalidPath));
        }
        finally
        {
            if (File.Exists(invalidPath))
            {
                File.Delete(invalidPath);
            }
        }
    }

    private static void WorldSessionsAreNumberedAndLogged()
    {
        var root = TemporaryDirectory();
        try
        {
            var appConfig = ObservationAppConfigLoader.Load(AppConfigPath());
            var simulationConfig = SimulationConfigLoader.Load(SimulationConfigPath());
            Directory.CreateDirectory(Path.Combine(root, "v0.14", "world-9999"));
            var store = new WorldSessionStore(root, appConfig, AppConfigPath());
            SequenceEqual(new[] { "v0.14" }, store.MaintenanceReport.DeletedReleaseVersions);
            True(!Directory.Exists(Path.Combine(root, "v0.14")));
            WorldSessionInfo firstInfo;
            TickResult tick;
            using (var first = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9000))
            {
                Equal(1, first.Info.WorldNumber);
                Equal("v0.2", first.Info.ReleaseVersion);
                Equal("v0.2", Directory.GetParent(first.Info.DirectoryPath)!.Name);
                Equal(9000L, first.Info.Seed);
                tick = first.AdvanceOneDay();
                firstInfo = first.Info;
            }

            True(!Directory.Exists(firstInfo.DirectoryPath));
            var archivePath = firstInfo.DirectoryPath + ".zip";
            True(File.Exists(archivePath));
            True(File.Exists(archivePath + ".sha256"));
            var expectedArchiveHash = File.ReadAllText(archivePath + ".sha256").Split(' ', 2)[0];
            using (var archiveStream = File.OpenRead(archivePath))
            {
                Equal(expectedArchiveHash, Convert.ToHexString(SHA256.HashData(archiveStream)));
            }
            var eventLines = ReadArchiveLines(archivePath, "events.jsonl");
            Equal(tick.Events.Count, eventLines.Length);
            foreach (var line in eventLines)
            {
                using var document = JsonDocument.Parse(line);
                Equal(firstInfo.WorldId, document.RootElement.GetProperty("worldId").GetString());
            }

            Equal(3, ReadArchiveLines(archivePath, "daily-stats.csv").Length);
            var diagnosticLines = ReadArchiveLines(archivePath, "diagnostics.jsonl");
            Equal(2, diagnosticLines.Length);
            using (var diagnostics = JsonDocument.Parse(diagnosticLines[^1]))
            {
                Equal(firstInfo.WorldId, diagnostics.RootElement.GetProperty("worldId").GetString());
                Equal(firstInfo.ReleaseVersion, diagnostics.RootElement.GetProperty("releaseVersion").GetString());
                True(diagnostics.RootElement.GetProperty("statistics").TryGetProperty("targetedActions", out _));
                True(diagnostics.RootElement.GetProperty("statistics").TryGetProperty("perception", out _));
                True(diagnostics.RootElement.GetProperty("statistics").TryGetProperty("worldPhase", out _));
                True(diagnostics.RootElement.GetProperty("statistics").TryGetProperty("settlements", out _));
                True(diagnostics.RootElement.GetProperty("statistics").TryGetProperty("violence", out _));
            }
            using (var run = JsonDocument.Parse(ReadArchiveText(archivePath, "run.json")))
            {
                Equal(5, run.RootElement.GetProperty("schemaVersion").GetInt32());
                True(run.RootElement.GetProperty("repositoryCommit").GetString()!.Length > 0);
                True(run.RootElement.GetProperty("repositoryTreeState").GetString()!.Length > 0);
                Equal(64, run.RootElement.GetProperty("simulationConfigSha256").GetString()!.Length);
                Equal(64, run.RootElement.GetProperty("observationAppConfigSha256").GetString()!.Length);
            }
            True(ArchiveContains(archivePath, "simulation-config.json"));
            True(ArchiveContains(archivePath, "observation-app-config.json"));

            using var second = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9000);
            Equal(2, second.Info.WorldNumber);
            Equal(9001L, second.Info.Seed);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void LoggingDoesNotChangeSimulationEvents()
    {
        var root = TemporaryDirectory();
        try
        {
            var appConfig = ObservationAppConfigLoader.Load(AppConfigPath());
            var simulationConfig = SimulationConfigLoader.Load(SimulationConfigPath());
            var store = new WorldSessionStore(root, appConfig, AppConfigPath());
            using var logged = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 7722);
            var baseline = new SimulationEngine(SimulationConfigLoader.Load(SimulationConfigPath()), logged.Info.Seed);
            for (var day = 0; day < 3; day++)
            {
                logged.AdvanceOneDay();
                baseline.AdvanceOneDay();
            }

            SequenceEqual(baseline.EventFingerprints(), logged.Engine.EventFingerprints());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void StatisticsChartRendersChangingSeries()
    {
        using var panel = new WorldStatisticsChartPanel
        {
            Size = new Size(560, 440),
            DaysPerYear = 365,
            Metrics = Enumerable.Range(0, 120)
                .Select(day => new WorldMetricPoint(
                    day,
                    200 + (int)Math.Round(30 * Math.Sin(day / 13d)) + day / 8,
                    1.2 + 0.25 * Math.Cos(day / 19d),
                    day < 60 ? WorldPhase.Generation : WorldPhase.Order,
                    day / 30,
                    Math.Clamp(day / 119d, 0, 1),
                    Array.Empty<ActionSelectionCount>()))
                .ToArray()
        };
        using var bitmap = new Bitmap(panel.Width, panel.Height);
        panel.DrawToBitmap(bitmap, panel.ClientRectangle);
        var sampledColors = new HashSet<int>();
        for (var y = 0; y < bitmap.Height; y += 8)
        {
            for (var x = 0; x < bitmap.Width; x += 8)
            {
                sampledColors.Add(bitmap.GetPixel(x, y).ToArgb());
            }
        }

        True(sampledColors.Count > 12);
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"world-sim-app-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string SimulationConfigPath() =>
        Path.Combine(AppContext.BaseDirectory, "simulation", "configs", "v0-default.json");

    private static string AppConfigPath() =>
        Path.Combine(AppContext.BaseDirectory, "simulation", "configs", "observation-app.json");

    private static string[] ReadArchiveLines(string archivePath, string entryName) =>
        ReadArchiveText(archivePath, entryName).Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);

    private static string ReadArchiveText(string archivePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry(entryName) ??
                    throw new InvalidOperationException($"Archive entry not found: {entryName}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static bool ArchiveContains(string archivePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return archive.GetEntry(entryName) is not null;
    }

    private static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed.");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException("Sequences differ.");
        }
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
    }
}
