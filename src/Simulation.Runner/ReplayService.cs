using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Core;
using Simulation.Core.Configuration;

namespace Simulation.Runner;

public sealed record ReplayExpectation(
    int CompletedTick,
    int Population,
    int EventCount,
    string EventStreamSha256,
    string FinalStateSha256);

public sealed record ReplayEnvelope(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string CodeVersion,
    string ConfigId,
    string ConfigurationSha256,
    long RunSeed,
    int Ticks,
    JsonElement[] ExternalInputs,
    JsonElement Configuration,
    ReplayExpectation Expected);

public sealed record ReplayActual(
    int CompletedTick,
    int Population,
    int EventCount,
    string EventStreamSha256,
    string FinalStateSha256);

public sealed record ReplayVerificationResult(
    bool IsMatch,
    bool CodeVersionMatches,
    string RecordedCodeVersion,
    string CurrentCodeVersion,
    ReplayActual Actual,
    IReadOnlyList<string> Differences);

public static class ReplayFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static ReplayEnvelope Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return JsonSerializer.Deserialize<ReplayEnvelope>(File.ReadAllText(path), Options)
                   ?? throw new InvalidDataException("Replay envelope cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid replay envelope at {path}: {exception.Message}", exception);
        }
    }

    public static void Save(string path, ReplayEnvelope replay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(replay);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new InvalidOperationException("Replay output has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(replay, Options), new UTF8Encoding(false));
        File.Move(temporaryPath, fullPath, true);
    }
}

public static class ReplayService
{
    public const int CurrentSchemaVersion = 1;

    public static string CurrentCodeVersion =>
        typeof(SimulationEngine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(SimulationEngine).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static ReplayEnvelope Record(string configurationPath, long runSeed, int ticks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), "Replay ticks cannot be negative.");
        }

        var configurationJson = File.ReadAllText(configurationPath);
        var configuration = SimulationConfigLoader.LoadJson(configurationJson, configurationPath);
        using var document = JsonDocument.Parse(configurationJson, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        var configurationElement = document.RootElement.Clone();
        var actual = Execute(configuration, runSeed, ticks);
        return new ReplayEnvelope(
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            CurrentCodeVersion,
            configuration.Id,
            HashJson(configurationElement),
            runSeed,
            ticks,
            Array.Empty<JsonElement>(),
            configurationElement,
            new ReplayExpectation(
                actual.CompletedTick,
                actual.Population,
                actual.EventCount,
                actual.EventStreamSha256,
                actual.FinalStateSha256));
    }

    public static ReplayEnvelope RecordToFile(string configurationPath, long runSeed, int ticks, string outputPath)
    {
        var replay = Record(configurationPath, runSeed, ticks);
        ReplayFile.Save(outputPath, replay);
        return replay;
    }

    public static ReplayVerificationResult Verify(string replayPath)
    {
        var replay = ReplayFile.Load(replayPath);
        ValidateEnvelope(replay);
        var configuration = SimulationConfigLoader.LoadJson(
            replay.Configuration.GetRawText(), $"{replayPath}#configuration");
        if (!string.Equals(configuration.Id, replay.ConfigId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Replay config id {replay.ConfigId} does not match embedded config id {configuration.Id}.");
        }

        var actual = Execute(configuration, replay.RunSeed, replay.Ticks);
        var differences = new List<string>();
        Compare("completedTick", replay.Expected.CompletedTick, actual.CompletedTick, differences);
        Compare("population", replay.Expected.Population, actual.Population, differences);
        Compare("eventCount", replay.Expected.EventCount, actual.EventCount, differences);
        Compare("eventStreamSha256", replay.Expected.EventStreamSha256, actual.EventStreamSha256, differences);
        Compare("finalStateSha256", replay.Expected.FinalStateSha256, actual.FinalStateSha256, differences);
        return new ReplayVerificationResult(
            differences.Count == 0,
            string.Equals(replay.CodeVersion, CurrentCodeVersion, StringComparison.Ordinal),
            replay.CodeVersion,
            CurrentCodeVersion,
            actual,
            differences);
    }

    private static ReplayActual Execute(SimulationConfig configuration, long runSeed, int ticks)
    {
        var engine = new SimulationEngine(configuration, runSeed);
        engine.AdvanceDays(ticks);
        var snapshot = engine.GetSnapshot(0);
        var eventFingerprints = engine.EventFingerprints();
        return new ReplayActual(
            snapshot.Tick,
            snapshot.Npcs.Count,
            eventFingerprints.Count,
            HashStrings(eventFingerprints),
            engine.DeterministicStateFingerprint());
    }

    private static void ValidateEnvelope(ReplayEnvelope replay)
    {
        if (replay.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported replay schema {replay.SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if (replay.Ticks < 0)
        {
            throw new InvalidDataException("Replay ticks cannot be negative.");
        }

        if (replay.ExternalInputs.Length != 0)
        {
            throw new InvalidDataException(
                "This v0.2.2 runner cannot apply external inputs; only an empty externalInputs array is supported.");
        }

        if (replay.Expected.CompletedTick != replay.Ticks)
        {
            throw new InvalidDataException("Replay expected.completedTick must equal ticks for a new-world run.");
        }

        var configurationHash = HashJson(replay.Configuration);
        if (!string.Equals(configurationHash, replay.ConfigurationSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Embedded configuration hash mismatch: expected {replay.ConfigurationSha256}, got {configurationHash}.");
        }
    }

    private static string HashJson(JsonElement element)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(element);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string HashStrings(IReadOnlyList<string> values)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Compare<T>(string name, T expected, T actual, ICollection<string> differences)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            differences.Add($"{name}: expected={expected}, actual={actual}");
        }
    }
}
