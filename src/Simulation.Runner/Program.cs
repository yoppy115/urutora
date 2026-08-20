using System.Globalization;

namespace Simulation.Runner;

internal static class Program
{
    private const string Usage = """
        World Sim deterministic replay runner

        Record:
          Simulation.Runner record --config <json> --seed <int64> --ticks <int32> --output <replay.json>

        Verify:
          Simulation.Runner verify --replay <replay.json>
        """;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args[1..]);
            return command switch
            {
                "record" => Record(options),
                "verify" => Verify(options),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"REPLAY_ERROR {exception.Message}");
            Console.Error.WriteLine(Usage);
            return 1;
        }
    }

    private static int Record(IReadOnlyDictionary<string, string> options)
    {
        RequireOnly(options, "--config", "--seed", "--ticks", "--output");
        var configPath = Required(options, "--config");
        var outputPath = Required(options, "--output");
        var seed = long.Parse(Required(options, "--seed"), NumberStyles.Integer, CultureInfo.InvariantCulture);
        var ticks = int.Parse(Required(options, "--ticks"), NumberStyles.Integer, CultureInfo.InvariantCulture);
        var replay = ReplayService.RecordToFile(configPath, seed, ticks, outputPath);
        Console.WriteLine(
            $"REPLAY_RECORDED path={Path.GetFullPath(outputPath)} seed={seed} ticks={ticks} " +
            $"events={replay.Expected.EventCount} eventSha256={replay.Expected.EventStreamSha256} " +
            $"stateSha256={replay.Expected.FinalStateSha256}");
        return 0;
    }

    private static int Verify(IReadOnlyDictionary<string, string> options)
    {
        RequireOnly(options, "--replay");
        var replayPath = Required(options, "--replay");
        var result = ReplayService.Verify(replayPath);
        if (!result.CodeVersionMatches)
        {
            Console.Error.WriteLine(
                $"REPLAY_VERSION_NOTICE recorded={result.RecordedCodeVersion} current={result.CurrentCodeVersion}");
        }

        if (!result.IsMatch)
        {
            foreach (var difference in result.Differences)
            {
                Console.Error.WriteLine($"REPLAY_DRIFT {difference}");
            }

            return 2;
        }

        Console.WriteLine(
            $"REPLAY_OK path={Path.GetFullPath(replayPath)} tick={result.Actual.CompletedTick} " +
            $"population={result.Actual.Population} events={result.Actual.EventCount} " +
            $"eventSha256={result.Actual.EventStreamSha256} stateSha256={result.Actual.FinalStateSha256}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Expected --option value near argument {index + 2}.");
            }

            if (!options.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Duplicate option: {args[index]}");
            }
        }

        return options;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required option: {name}");

    private static void RequireOnly(IReadOnlyDictionary<string, string> options, params string[] allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var unexpected = options.Keys.Where(key => !allowedSet.Contains(key)).ToArray();
        if (unexpected.Length > 0)
        {
            throw new ArgumentException($"Unexpected option(s): {string.Join(", ", unexpected)}");
        }
    }
}
