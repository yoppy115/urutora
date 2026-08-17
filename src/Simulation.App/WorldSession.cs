using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Core;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.App;

public sealed record WorldSessionInfo(
    string ReleaseVersion,
    int WorldNumber,
    string WorldId,
    long Seed,
    string DirectoryPath,
    DateTimeOffset CreatedAtUtc);

public sealed record WorldMetricPoint(
    int Tick,
    int Population,
    double AverageAgeYears,
    WorldPhase Phase,
    int SettlementCount,
    double AffiliationRate,
    IReadOnlyList<ActionSelectionCount> ActionSelections);

public enum WorldCompletionReason
{
    Manual,
    TargetYearReached,
    Superseded
}

public sealed class WorldSessionStore
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly WorldLogRetention _retention;
    private readonly ObservationAppConfig _appConfig;
    private readonly string _appConfigPath;

    public WorldSessionStore(
        string logRoot,
        ObservationAppConfig appConfig,
        string appConfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logRoot);
        _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        _appConfig.Validate();
        _retention = new WorldLogRetention(
            Path.GetFullPath(logRoot),
            ReleaseIdentity.VersionDirectoryName,
            _appConfig);
        _appConfigPath = appConfigPath;
        MaintenanceReport = _retention.MaintainPastLogs();
    }

    public string LogRoot => _retention.ReleaseLogRoot;
    public WorldLogMaintenanceReport MaintenanceReport { get; }

    public WorldSession CreateNextWorld(
        SimulationConfig simulationConfig,
        string simulationConfigPath,
        long baseSeed)
    {
        ArgumentNullException.ThrowIfNull(simulationConfig);
        simulationConfig.Validate();
        Directory.CreateDirectory(LogRoot);

        var worldNumber = _retention.NextWorldNumber();
        var worldId = _appConfig.WorldDirectoryPrefix +
                      worldNumber.ToString($"D{_appConfig.WorldNumberPadding}", CultureInfo.InvariantCulture);
        var directoryPath = Path.Combine(LogRoot, worldId);
        while (Directory.Exists(directoryPath))
        {
            worldNumber++;
            worldId = _appConfig.WorldDirectoryPrefix +
                      worldNumber.ToString($"D{_appConfig.WorldNumberPadding}", CultureInfo.InvariantCulture);
            directoryPath = Path.Combine(LogRoot, worldId);
        }

        Directory.CreateDirectory(directoryPath);
        var seed = checked(baseSeed + (worldNumber - 1L) * _appConfig.SeedIncrement);
        var createdAtUtc = DateTimeOffset.UtcNow;
        var info = new WorldSessionInfo(
            ReleaseIdentity.VersionDirectoryName, worldNumber, worldId, seed, directoryPath, createdAtUtc);
        var engine = new SimulationEngine(simulationConfig, seed);
        var logger = new WorldLogWriter(
            info,
            simulationConfig,
            simulationConfigPath,
            _appConfig,
            _appConfigPath,
            MetadataJsonOptions);
        return new WorldSession(
            info,
            engine,
            logger,
            _appConfig.ChartMaximumPoints,
            _appConfig.DiagnosticsIntervalDays,
            () => _retention.ArchiveCompletedWorld(directoryPath));
    }
}

public sealed class WorldSession : IDisposable
{
    private readonly object _lifecycleGate = new();
    private readonly WorldLogWriter _logger;
    private readonly int _chartMaximumPoints;
    private readonly int _diagnosticsIntervalDays;
    private readonly Action _onCompleted;
    private readonly List<WorldMetricPoint> _metrics = new();
    private bool _disposed;
    private bool _completionCommitted;
    private bool _archiveCompleted;

    internal WorldSession(
        WorldSessionInfo info,
        SimulationEngine engine,
        WorldLogWriter logger,
        int chartMaximumPoints,
        int diagnosticsIntervalDays,
        Action onCompleted)
    {
        Info = info;
        Engine = engine;
        _logger = logger;
        _chartMaximumPoints = chartMaximumPoints;
        _diagnosticsIntervalDays = diagnosticsIntervalDays;
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        var observation = Engine.GetDailyObservation();
        AddMetric(observation);
        _logger.WriteInitial(observation, Engine.GetWorldStatistics());
    }

    public WorldSessionInfo Info { get; }
    public SimulationEngine Engine { get; }
    public IReadOnlyList<WorldMetricPoint> Metrics => _metrics.ToArray();
    public int CurrentTick { get; private set; }
    public bool IsCompleted => _completionCommitted;

    public TickResult AdvanceOneDay()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var result = Engine.AdvanceOneDay();
            var observation = Engine.GetDailyObservation();
            AddMetric(observation);
            var diagnostics = observation.Tick % _diagnosticsIntervalDays == 0
                ? Engine.GetWorldStatistics()
                : null;
            _logger.Append(result, observation, diagnostics);
            CurrentTick = checked(result.CompletedTick + 1);
            return result;
        }
    }

    public void Complete(WorldCompletionReason reason)
    {
        lock (_lifecycleGate)
        {
            if (!_completionCommitted)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _logger.Complete(CurrentTick, reason, Engine.GetWorldStatistics());
                _completionCommitted = true;
                _disposed = true;
            }

            if (!_archiveCompleted)
            {
                _onCompleted();
                _archiveCompleted = true;
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _logger.Dispose();
        }
    }

    private void AddMetric(DailyObservationProjection statistics)
    {
        _metrics.Add(new WorldMetricPoint(
            statistics.Tick,
            statistics.Population,
            statistics.AverageAgeYears,
            statistics.CurrentPhase,
            statistics.ActiveSettlementCount,
            statistics.Population == 0 ? 0 : (double)statistics.AffiliatedPopulation / statistics.Population,
            statistics.ActionSelections));
        if (_metrics.Count > _chartMaximumPoints)
        {
            _metrics.RemoveRange(0, _metrics.Count - _chartMaximumPoints);
        }
    }
}

internal sealed class WorldLogWriter : IDisposable
{
    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WorldSessionInfo _info;
    private readonly int _daysPerYear;
    private readonly StreamWriter _eventWriter;
    private readonly StreamWriter _statisticsWriter;
    private readonly StreamWriter _diagnosticsWriter;
    private readonly int _flushIntervalDays;
    private readonly JsonSerializerOptions _metadataJsonOptions;
    private int _daysSinceFlush;
    private bool _disposed;
    private bool _completionWritten;
    private int? _lastDiagnosticsTick;

    public WorldLogWriter(
        WorldSessionInfo info,
        SimulationConfig simulationConfig,
        string simulationConfigPath,
        ObservationAppConfig appConfig,
        string appConfigPath,
        JsonSerializerOptions metadataJsonOptions)
    {
        _info = info;
        _daysPerYear = simulationConfig.World.DaysPerYear;
        _flushIntervalDays = appConfig.LogFlushIntervalDays;
        _metadataJsonOptions = metadataJsonOptions;
        var simulationConfigSnapshotPath = Path.Combine(info.DirectoryPath, "simulation-config.json");
        var appConfigSnapshotPath = Path.Combine(info.DirectoryPath, "observation-app-config.json");
        CopyOrSerialize(
            simulationConfigPath,
            simulationConfigSnapshotPath,
            simulationConfig,
            metadataJsonOptions);
        CopyOrSerialize(
            appConfigPath,
            appConfigSnapshotPath,
            appConfig,
            metadataJsonOptions);

        var runMetadata = new WorldRunMetadata(
            5,
            info.ReleaseVersion,
            info.WorldNumber,
            info.WorldId,
            info.Seed,
            simulationConfig.Id,
            info.CreatedAtUtc,
            Environment.Version.ToString(),
            typeof(SimulationEngine).Assembly.GetName().Version?.ToString() ?? "unknown",
            ReleaseIdentity.InformationalVersion,
            ReleaseIdentity.RepositoryCommit,
            ReleaseIdentity.RepositoryTreeState,
            ComputeSha256(simulationConfigSnapshotPath),
            ComputeSha256(appConfigSnapshotPath));
        File.WriteAllText(
            Path.Combine(info.DirectoryPath, "run.json"),
            JsonSerializer.Serialize(runMetadata, metadataJsonOptions),
            new UTF8Encoding(false));

        _eventWriter = CreateWriter(Path.Combine(info.DirectoryPath, "events.jsonl"));
        _statisticsWriter = CreateWriter(Path.Combine(info.DirectoryPath, "daily-stats.csv"));
        _diagnosticsWriter = CreateWriter(Path.Combine(info.DirectoryPath, "diagnostics.jsonl"));
        var actions = Enum.GetValues<ActionKind>().Select(item => item.ToString());
        _statisticsWriter.WriteLine("tick,year,day,population,minimumPopulation,averageAgeYears," +
                                    "worldPhase,settlementCount,affiliatedPopulation,unaffiliatedPopulation,affiliationRate," +
                                    "populationCv,demographicImbalance,stabilityConsecutiveDays," +
                                    string.Join(',', actions.Select(item => $"selected{item}")) +
                                    ",positionInvalidations,subjectPurges,heldInformationEvictions," +
                                    "heldInformationTotal,heldInformationAverage,heldInformationMaximum," +
                                    "restActionRate,averageRestNeed,averageSelectedRestNeed,averageSelectedRestPressure," +
                                    "activeSettlementAverageSupport,totalLowSupportDays,armedSettlementCount," +
                                    "invasionStartPrevented");
    }

    public void WriteInitial(DailyObservationProjection statistics, WorldStatisticsProjection diagnostics)
    {
        WriteStatistics(statistics);
        WriteDiagnostics(diagnostics);
        _statisticsWriter.Flush();
        _diagnosticsWriter.Flush();
    }

    public void Append(
        TickResult tick,
        DailyObservationProjection statistics,
        WorldStatisticsProjection? diagnostics)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var simulationEvent in tick.Events)
        {
            var entry = new WorldEventLogEntry(
                4, _info.ReleaseVersion, _info.WorldNumber, _info.WorldId, _info.Seed, simulationEvent);
            _eventWriter.WriteLine(JsonSerializer.Serialize(entry, EventJsonOptions));
        }

        WriteStatistics(statistics);
        if (diagnostics is not null)
        {
            WriteDiagnostics(diagnostics);
        }
        _daysSinceFlush++;
        if (_daysSinceFlush >= _flushIntervalDays)
        {
            Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
        _eventWriter.Dispose();
        _statisticsWriter.Dispose();
        _diagnosticsWriter.Dispose();
    }

    public void Complete(
        int finalTick,
        WorldCompletionReason reason,
        WorldStatisticsProjection finalStatistics)
    {
        if (_completionWritten)
        {
            return;
        }

        if (!_disposed)
        {
            if (_lastDiagnosticsTick != finalStatistics.Tick)
            {
                WriteDiagnostics(finalStatistics);
            }
            Dispose();
        }

        var record = new WorldCompletionRecord(
            1,
            _info.ReleaseVersion,
            _info.WorldNumber,
            _info.WorldId,
            finalTick,
            reason.ToString(),
            DateTimeOffset.UtcNow);
        var completionPath = Path.Combine(_info.DirectoryPath, WorldLogRetention.CompletionFileName);
        var temporaryPath = completionPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(record, _metadataJsonOptions),
            new UTF8Encoding(false));
        File.Move(temporaryPath, completionPath, true);
        _completionWritten = true;
    }

    private void WriteStatistics(DailyObservationProjection statistics)
    {
        var counts = statistics.ActionSelections.ToDictionary(item => item.Action, item => item.Count);
        var year = statistics.Tick / _daysPerYear;
        var day = statistics.Tick % _daysPerYear + 1;
        var values = Enum.GetValues<ActionKind>()
            .Select(action => counts.GetValueOrDefault(action).ToString(CultureInfo.InvariantCulture));
        _statisticsWriter.WriteLine(string.Join(',', new[]
        {
            statistics.Tick.ToString(CultureInfo.InvariantCulture),
            year.ToString(CultureInfo.InvariantCulture),
            day.ToString(CultureInfo.InvariantCulture),
            statistics.Population.ToString(CultureInfo.InvariantCulture),
            statistics.MinimumPopulation.ToString(CultureInfo.InvariantCulture),
            statistics.AverageAgeYears.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.CurrentPhase.ToString(),
            statistics.ActiveSettlementCount.ToString(CultureInfo.InvariantCulture),
            statistics.AffiliatedPopulation.ToString(CultureInfo.InvariantCulture),
            (statistics.Population - statistics.AffiliatedPopulation).ToString(CultureInfo.InvariantCulture),
            (statistics.Population == 0 ? 0 : (double)statistics.AffiliatedPopulation / statistics.Population)
                .ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.PopulationCv.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.DemographicImbalance.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.StabilityConsecutiveDays.ToString(CultureInfo.InvariantCulture)
        }.Concat(values).Concat(new[]
        {
            statistics.Perception.PositionInvalidations.ToString(CultureInfo.InvariantCulture),
            statistics.Perception.SubjectPurges.ToString(CultureInfo.InvariantCulture),
            statistics.Perception.HeldInformationEvictions.ToString(CultureInfo.InvariantCulture),
            statistics.Perception.HeldInformationTotal.ToString(CultureInfo.InvariantCulture),
            statistics.Perception.HeldInformationAverage.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.Perception.HeldInformationMaximum.ToString(CultureInfo.InvariantCulture),
            statistics.RestActionRate.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.AverageRestNeed.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.AverageSelectedRestNeed.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.AverageSelectedRestPressure.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.ActiveSettlementAverageSupport.ToString("0.000000", CultureInfo.InvariantCulture),
            statistics.TotalLowSupportDays.ToString(CultureInfo.InvariantCulture),
            statistics.ArmedSettlementCount.ToString(CultureInfo.InvariantCulture),
            statistics.InvasionStartPrevented.ToString(CultureInfo.InvariantCulture)
        })));
    }

    private void Flush()
    {
        _eventWriter.Flush();
        _statisticsWriter.Flush();
        _diagnosticsWriter.Flush();
        _daysSinceFlush = 0;
    }

    private void WriteDiagnostics(WorldStatisticsProjection statistics)
    {
        var entry = new WorldStatisticsLogEntry(
            5, _info.ReleaseVersion, _info.WorldNumber, _info.WorldId, _info.Seed, statistics);
        _diagnosticsWriter.WriteLine(JsonSerializer.Serialize(entry, EventJsonOptions));
        _lastDiagnosticsTick = statistics.Tick;
    }

    private static StreamWriter CreateWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
    }

    private static void CopyOrSerialize<T>(
        string sourcePath,
        string destinationPath,
        T value,
        JsonSerializerOptions options)
    {
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, true);
            return;
        }

        File.WriteAllText(destinationPath, JsonSerializer.Serialize(value, options), new UTF8Encoding(false));
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record WorldRunMetadata(
        int SchemaVersion,
        string ReleaseVersion,
        int WorldNumber,
        string WorldId,
        long Seed,
        string ConfigId,
        DateTimeOffset CreatedAtUtc,
        string RuntimeVersion,
        string CoreVersion,
        string InformationalVersion,
        string RepositoryCommit,
        string RepositoryTreeState,
        string SimulationConfigSha256,
        string ObservationAppConfigSha256);

    private sealed record WorldEventLogEntry(
        int SchemaVersion,
        string ReleaseVersion,
        int WorldNumber,
        string WorldId,
        long Seed,
        SimulationEvent Event);

    private sealed record WorldStatisticsLogEntry(
        int SchemaVersion,
        string ReleaseVersion,
        int WorldNumber,
        string WorldId,
        long Seed,
        WorldStatisticsProjection Statistics);

    private sealed record WorldCompletionRecord(
        int SchemaVersion,
        string ReleaseVersion,
        int WorldNumber,
        string WorldId,
        int FinalTick,
        string Reason,
        DateTimeOffset CompletedAtUtc);
}
