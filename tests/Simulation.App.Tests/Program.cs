using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Reflection;
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
        ("unfinished Worlds are preserved without completed archives", UnfinishedWorldsArePreserved),
        ("target-year batch run counts completed Worlds", TargetYearBatchRunCountsWorlds),
        ("target-year batch archives each numbered World", TargetYearBatchArchivesNumberedWorlds),
        ("world logging does not change Simulation events", LoggingDoesNotChangeSimulationEvents),
        ("statistics chart renders changing series", StatisticsChartRendersChangingSeries),
        ("ConceptMark renders as a concept-colored flag", ConceptMarkRendersAsColoredFlag),
        ("Settlement palette expands and recycles dissolved colors", SettlementPaletteExpandsAndRecycles),
        ("Settlement center click takes priority over NPC click", SettlementCenterClickTakesPriority),
        ("Social display hides dissolved Settlements and scopes Friction", SocialDisplayFiltersSettlementsAndFriction)
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
        Equal(5, config.SchemaVersion);
        Equal(1, config.SeedIncrement);
        Equal(500, config.NpcActionHistoryDisplayLimit);
        Equal(0.5, config.AgeDistributionBinYears);
        Equal(10, config.LogFlushIntervalDays);
        Equal(2, config.AutomaticAdvanceWorkSliceDays);
        Equal(15, config.AutomaticAdvanceCooldownMilliseconds);
        True(config.ArchiveCompletedWorldLogs);
        True(config.DeleteOtherReleaseVersionLogs);
        var invalidPath = Path.Combine(Path.GetTempPath(), $"world-sim-app-invalid-{Guid.NewGuid():N}.json");
        try
        {
            var json = File.ReadAllText(AppConfigPath())
                .Replace("\"schemaVersion\": 5", "\"schemaVersion\": 5, \"unknown\": true", StringComparison.Ordinal);
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
                Equal("v0.2.3", first.Info.ReleaseVersion);
                Equal("v0.2.3", Directory.GetParent(first.Info.DirectoryPath)!.Name);
                Equal(9000L, first.Info.Seed);
                tick = first.AdvanceOneDay();
                firstInfo = first.Info;
                first.Complete(WorldCompletionReason.Manual);
                True(first.IsCompleted);
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
            using (var completion = JsonDocument.Parse(ReadArchiveText(archivePath, "completion.json")))
            {
                Equal(1, completion.RootElement.GetProperty("schemaVersion").GetInt32());
                Equal(1, completion.RootElement.GetProperty("finalTick").GetInt32());
                Equal(nameof(WorldCompletionReason.Manual),
                    completion.RootElement.GetProperty("reason").GetString());
            }

            using var second = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9000);
            Equal(2, second.Info.WorldNumber);
            Equal(9001L, second.Info.Seed);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void UnfinishedWorldsArePreserved()
    {
        var root = TemporaryDirectory();
        try
        {
            var appConfig = ObservationAppConfigLoader.Load(AppConfigPath());
            var simulationConfig = SimulationConfigLoader.Load(SimulationConfigPath());
            WorldSessionInfo unfinishedInfo;
            var firstStore = new WorldSessionStore(root, appConfig, AppConfigPath());
            using (var unfinished = firstStore.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9100))
            {
                unfinished.AdvanceOneDay();
                unfinishedInfo = unfinished.Info;
            }

            True(Directory.Exists(unfinishedInfo.DirectoryPath));
            True(!File.Exists(unfinishedInfo.DirectoryPath + ".zip"));
            True(!File.Exists(Path.Combine(unfinishedInfo.DirectoryPath, "completion.json")));

            var restartedStore = new WorldSessionStore(root, appConfig, AppConfigPath());
            Equal(0, restartedStore.MaintenanceReport.ArchivedWorlds.Count);
            True(Directory.Exists(unfinishedInfo.DirectoryPath));
            using var next = restartedStore.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9100);
            Equal(2, next.Info.WorldNumber);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void TargetYearBatchRunCountsWorlds()
    {
        var batch = new WorldBatchRun();
        batch.Start(5, 3, 365);
        Equal(1825, batch.TargetTick);
        Equal(3, batch.RemainingWorlds);
        True(!batch.HasReachedTarget(1824));
        True(batch.HasReachedTarget(1825));
        True(batch.RecordWorldCompleted());
        Equal(1, batch.CompletedWorlds);
        Equal(2, batch.RemainingWorlds);
        True(batch.RecordWorldCompleted());
        True(!batch.RecordWorldCompleted());
        True(!batch.IsActive);
        Equal(0, batch.RemainingWorlds);
    }

    private static void TargetYearBatchArchivesNumberedWorlds()
    {
        var root = TemporaryDirectory();
        try
        {
            var appConfig = ObservationAppConfigLoader.Load(AppConfigPath());
            var simulationConfig = SimulationConfigLoader.Load(SimulationConfigPath());
            var store = new WorldSessionStore(root, appConfig, AppConfigPath());
            var batch = new WorldBatchRun();
            batch.Start(1, 2, 1);
            var world = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9200);
            while (batch.IsActive)
            {
                while (!batch.HasReachedTarget(world.CurrentTick))
                {
                    world.AdvanceOneDay();
                }

                world.Complete(WorldCompletionReason.TargetYearReached);
                var continueBatch = batch.RecordWorldCompleted();
                world.Dispose();
                if (continueBatch)
                {
                    world = store.CreateNextWorld(simulationConfig, SimulationConfigPath(), 9200);
                }
            }

            Equal(2, Directory.EnumerateFiles(store.LogRoot, "world-*.zip").Count());
            Equal(0, Directory.EnumerateDirectories(store.LogRoot, "world-*").Count());
            using var firstCompletion = JsonDocument.Parse(
                ReadArchiveText(Path.Combine(store.LogRoot, "world-0001.zip"), "completion.json"));
            using var secondCompletion = JsonDocument.Parse(
                ReadArchiveText(Path.Combine(store.LogRoot, "world-0002.zip"), "completion.json"));
            Equal(1, firstCompletion.RootElement.GetProperty("finalTick").GetInt32());
            Equal(1, secondCompletion.RootElement.GetProperty("finalTick").GetInt32());
            Equal(nameof(WorldCompletionReason.TargetYearReached),
                secondCompletion.RootElement.GetProperty("reason").GetString());
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

    private static void ConceptMarkRendersAsColoredFlag()
    {
        using var panel = new WorldMapPanel
        {
            Size = new Size(660, 660),
            Snapshot = new SimulationSnapshot(
                0,
                365,
                10,
                10,
                WorldPhase.Generation,
                new[]
                {
                    new NpcProjection(
                        1,
                        new Position(4, 4),
                        new HashSet<ConceptKind> { ConceptKind.Struggle },
                        new HashSet<ConceptKind>(),
                        1,
                        null)
                },
                Array.Empty<LandmarkProjection>(),
                new[]
                {
                    new SettlementProjection(1, new Position(4, 4), 3, 7, 0, true, 1, 0)
                },
                Array.Empty<InvasionProjection>(),
                Array.Empty<SimulationEvent>())
        };
        using var bitmap = new Bitmap(panel.Width, panel.Height);
        panel.DrawToBitmap(bitmap, panel.ClientRectangle);
        var conceptColor = Color.FromArgb(224, 72, 72).ToArgb();
        var conceptPixels = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == conceptColor)
                {
                    conceptPixels++;
                }
            }
        }

        True(conceptPixels > 10);
    }

    private static void SettlementPaletteExpandsAndRecycles()
    {
        var allocator = new SettlementColorAllocator();
        var settlements = Enumerable.Range(1, SettlementColorAllocator.PaletteSize)
            .Select(id => Settlement(id, true))
            .ToArray();
        allocator.Synchronize(settlements);
        var colors = settlements.Select(item => allocator.ColorFor(item.Id).ToArgb()).ToArray();
        Equal(SettlementColorAllocator.PaletteSize, colors.Distinct().Count());

        var releasedColor = allocator.ColorFor(1);
        allocator.Synchronize(settlements
            .Select(item => item.Id == 1 ? item with { IsActive = false } : item)
            .Append(Settlement(61, true))
            .ToArray());
        Equal(releasedColor.ToArgb(), allocator.ColorFor(61).ToArgb());
    }

    private static void SettlementCenterClickTakesPriority()
    {
        using var panel = new WorldMapPanel
        {
            Size = new Size(660, 660),
            Snapshot = new SimulationSnapshot(
                0,
                365,
                10,
                10,
                WorldPhase.Generation,
                new[]
                {
                    new NpcProjection(1, new Position(4, 4), new HashSet<ConceptKind>(),
                        new HashSet<ConceptKind>(), 1, null)
                },
                Array.Empty<LandmarkProjection>(),
                new[] { Settlement(1, true) },
                Array.Empty<InvasionProjection>(),
                Array.Empty<SimulationEvent>())
        };
        var settlementSelections = 0;
        var npcSelections = 0;
        panel.SettlementSelected += (_, args) =>
        {
            Equal(1, args.SettlementId);
            settlementSelections++;
        };
        panel.NpcSelected += (_, _) => npcSelections++;
        var cell = (panel.ClientSize.Width - panel.Padding.Horizontal) / 10f;
        var click = new Point(
            (int)Math.Round(panel.Padding.Left + 4.5f * cell),
            (int)Math.Round(panel.Padding.Top + 4.5f * cell));
        var method = typeof(WorldMapPanel).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WorldMapPanel.OnMouseClick was not found.");
        method.Invoke(panel, new object[] { new MouseEventArgs(MouseButtons.Left, 1, click.X, click.Y, 0) });

        Equal(1, settlementSelections);
        Equal(0, npcSelections);
        Equal(1, panel.SelectedSettlementId!.Value);
    }

    private static void SocialDisplayFiltersSettlementsAndFriction()
    {
        var settlements = new[]
        {
            SettlementStatistics(1, active: true),
            SettlementStatistics(2, active: false),
            SettlementStatistics(3, active: false, dissolvedTick: 20)
        };
        SequenceEqual(new[] { 1, 2 },
            ObservationDisplayPolicy.VisibleSocialSettlements(settlements).Select(item => item.Id));

        var frictions = new[]
        {
            new FrictionStatistics(1, 2, 3, 2, 0, 1, 10),
            new FrictionStatistics(2, 3, 8, 4, 1, 0, 12),
            new FrictionStatistics(1, 3, 5, 3, 1, 2, 14)
        };
        SequenceEqual(new[] { (1, 3), (1, 2) },
            ObservationDisplayPolicy.FrictionsForSettlement(frictions, 1)
                .Select(item => (item.FirstSettlementId, item.SecondSettlementId)));

        var events = new[]
        {
            new SimulationEvent("friction", 1, 0, SimulationEventType.SettlementFrictionChanged,
                null, null, null, true, "pair=1:2"),
            new SimulationEvent("rest", 1, 1, SimulationEventType.Rest, 1, null,
                new Position(1, 1), true, string.Empty)
        };
        SequenceEqual(new[] { SimulationEventType.Rest },
            ObservationDisplayPolicy.VisibleRecentEvents(events).Select(item => item.Type));
    }

    private static SettlementProjection Settlement(int id, bool active) =>
        new(id, id == 1 ? new Position(4, 4) : new Position(id % 10, id % 10), 2, 7, id, active, 1, 0);

    private static SettlementStatistics SettlementStatistics(int id, bool active, int? dissolvedTick = null) =>
        new(id, new Position(id, id), id, 2, active, 4, 0.2, 0.4, 0.1, 0,
            dissolvedTick, dissolvedTick.HasValue ? "test" : null, null);

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
