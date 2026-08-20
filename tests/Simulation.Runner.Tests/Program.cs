using System.Text.Json;
using Simulation.Runner;

namespace Simulation.Runner.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        ("recorded replay verifies", RecordedReplayVerifies),
        ("digest drift is rejected", DigestDriftIsRejected),
        ("configuration tampering is rejected", ConfigurationTamperingIsRejected)
    };

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

        Console.WriteLine(
            $"RUNNER_TEST_SUMMARY total={Tests.Length} passed={Tests.Length - failures.Count} failed={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void RecordedReplayVerifies()
    {
        WithReplayFile((path, configPath) =>
        {
            var recorded = ReplayService.RecordToFile(configPath, 8147291, 3, path);
            var verification = ReplayService.Verify(path);
            True(recorded.SchemaVersion == ReplayService.CurrentSchemaVersion);
            True(recorded.ExternalInputs.Length == 0);
            True(verification.IsMatch, string.Join(Environment.NewLine, verification.Differences));
            True(verification.Actual.EventCount > 0, "Replay produced no events.");
        });
    }

    private static void DigestDriftIsRejected()
    {
        WithReplayFile((path, configPath) =>
        {
            var recorded = ReplayService.Record(configPath, -915, 2);
            var tampered = recorded with
            {
                Expected = recorded.Expected with { EventStreamSha256 = new string('0', 64) }
            };
            ReplayFile.Save(path, tampered);
            var verification = ReplayService.Verify(path);
            True(!verification.IsMatch, "Digest drift was accepted.");
            True(verification.Differences.Any(item => item.StartsWith("eventStreamSha256", StringComparison.Ordinal)));
        });
    }

    private static void ConfigurationTamperingIsRejected()
    {
        WithReplayFile((path, configPath) =>
        {
            var recorded = ReplayService.Record(configPath, 27, 1);
            using var document = JsonDocument.Parse(recorded.Configuration.GetRawText().Replace(
                "\"initialPopulation\": 200",
                "\"initialPopulation\": 199",
                StringComparison.Ordinal));
            var tampered = recorded with { Configuration = document.RootElement.Clone() };
            ReplayFile.Save(path, tampered);
            Throws<InvalidDataException>(() => ReplayService.Verify(path));
        });
    }

    private static void WithReplayFile(Action<string, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"world-sim-runner-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(
                Path.Combine(directory, "run.replay.json"),
                Path.Combine(AppContext.BaseDirectory, "simulation", "configs", "v0-default.json"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void True(bool condition, string message = "Assertion failed.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
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
