using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.Core.Configuration;

namespace Simulation.App;

public sealed class ObservationAppConfig
{
    public int SchemaVersion { get; set; }
    public long DefaultSeed { get; set; }
    public long SeedIncrement { get; set; }
    public string LogDirectory { get; set; } = string.Empty;
    public string WorldDirectoryPrefix { get; set; } = string.Empty;
    public int WorldNumberPadding { get; set; }
    public int RecentEventDisplayLimit { get; set; }
    public int ChartMaximumPoints { get; set; }
    public int NpcActionHistoryDisplayLimit { get; set; }
    public double AgeDistributionBinYears { get; set; }
    public int LogFlushIntervalDays { get; set; }
    public int AutomaticAdvanceWorkSliceDays { get; set; }
    public int AutomaticAdvanceCooldownMilliseconds { get; set; }
    public bool ArchiveCompletedWorldLogs { get; set; }
    public bool DeleteOtherReleaseVersionLogs { get; set; }

    public void Validate()
    {
        var errors = new List<string>();
        Require(SchemaVersion == 5, "schemaVersion must be 5.", errors);
        Require(SeedIncrement != 0, "seedIncrement cannot be zero.", errors);
        Require(IsSafeRelativeDirectory(LogDirectory),
            "logDirectory must be a simple relative path inside the repository.", errors);
        Require(!string.IsNullOrWhiteSpace(WorldDirectoryPrefix) &&
                WorldDirectoryPrefix.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                !WorldDirectoryPrefix.Contains(Path.DirectorySeparatorChar) &&
                !WorldDirectoryPrefix.Contains(Path.AltDirectorySeparatorChar),
            "worldDirectoryPrefix must be a valid directory-name prefix.", errors);
        Require(WorldNumberPadding is >= 1 and <= 9,
            "worldNumberPadding must be within 1..9.", errors);
        Require(RecentEventDisplayLimit > 0,
            "recentEventDisplayLimit must be positive.", errors);
        Require(ChartMaximumPoints >= 2,
            "chartMaximumPoints must be at least 2.", errors);
        Require(NpcActionHistoryDisplayLimit > 0,
            "npcActionHistoryDisplayLimit must be positive.", errors);
        Require(double.IsFinite(AgeDistributionBinYears) && AgeDistributionBinYears is > 0 and <= 10,
            "ageDistributionBinYears must be finite and within (0, 10].", errors);
        Require(LogFlushIntervalDays is >= 1 and <= 365,
            "logFlushIntervalDays must be within 1..365.", errors);
        Require(AutomaticAdvanceWorkSliceDays is >= 1 and <= 50,
            "automaticAdvanceWorkSliceDays must be within 1..50.", errors);
        Require(AutomaticAdvanceCooldownMilliseconds is >= 0 and <= 1000,
            "automaticAdvanceCooldownMilliseconds must be within 0..1000.", errors);
        if (errors.Count > 0)
        {
            throw new ConfigurationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static bool IsSafeRelativeDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        {
            return false;
        }

        return value.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static void Require(bool condition, string message, ICollection<string> errors)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }
}

public static class ObservationAppConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ObservationAppConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var config = JsonSerializer.Deserialize<ObservationAppConfig>(File.ReadAllText(path), Options)
                ?? throw new ConfigurationException("Observation App configuration root cannot be null.");
            config.Validate();
            return config;
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException(
                $"Invalid Observation App configuration JSON at {path}: {exception.Message}", exception);
        }
    }
}
