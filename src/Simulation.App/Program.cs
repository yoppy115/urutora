using Simulation.Core;
using Simulation.Core.Configuration;

namespace Simulation.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var repositoryRoot = RepositoryLocator.Find(ReadString(args, "--repository-root"));
            var configPath = ReadString(args, "--config") ??
                             Path.Combine(repositoryRoot, "simulation", "configs", "v0-default.json");
            var appConfigPath = ReadString(args, "--app-config") ??
                                Path.Combine(repositoryRoot, "simulation", "configs", "observation-app.json");
            var appConfig = ObservationAppConfigLoader.Load(appConfigPath);
            var seed = ReadLong(args, "--seed") ?? appConfig.DefaultSeed;
            var config = SimulationConfigLoader.Load(configPath);
            ReleaseIdentity.ValidateSimulationConfig(config.Id);

            var logRoot = ReadString(args, "--log-root") ?? Path.Combine(repositoryRoot, appConfig.LogDirectory);
            if (args.Contains("--maintain-logs", StringComparer.Ordinal))
            {
                var maintenanceStore = new WorldSessionStore(logRoot, appConfig, appConfigPath);
                Console.WriteLine(
                    $"LOG_MAINTENANCE_OK archived={maintenanceStore.MaintenanceReport.ArchivedWorlds.Count} " +
                    $"deletedVersions={maintenanceStore.MaintenanceReport.DeletedReleaseVersions.Count} " +
                    $"root={maintenanceStore.LogRoot}");
                return 0;
            }

            if (args.Contains("--headless", StringComparer.Ordinal))
            {
                var engine = new SimulationEngine(config, seed);
                engine.AdvanceDays(ReadInt(args, "--ticks") ?? 10);
                var snapshot = engine.GetSnapshot();
                Console.WriteLine($"HEADLESS_OK seed={seed} tick={snapshot.Tick} population={snapshot.Npcs.Count} events={snapshot.RecentEvents.Count}");
                return 0;
            }

            var worldStore = new WorldSessionStore(logRoot, appConfig, appConfigPath);
            var initialWorld = worldStore.CreateNextWorld(config, configPath, seed);
            ApplicationConfiguration.Initialize();
            using var form = new MainForm(worldStore, initialWorld, config, configPath, seed, appConfig);
            if (args.Contains("--ui-smoke", StringComparer.Ordinal))
            {
                var closeTimer = new System.Windows.Forms.Timer { Interval = 500 };
                form.Tag = closeTimer;
                closeTimer.Tick += async (_, _) =>
                {
                    closeTimer.Stop();
                    try
                    {
                        await form.RunUiSmokeChecksAsync();
                    }
                    catch (Exception exception)
                    {
                        form.SmokeFailure = exception;
                    }
                    finally
                    {
                        closeTimer.Dispose();
                        form.Tag = null;
                        form.Close();
                    }
                };
                closeTimer.Start();
            }

            Application.Run(form);
            if (form.SmokeFailure is not null)
            {
                throw new InvalidOperationException("UI smoke check failed.", form.SmokeFailure);
            }

            return 0;
        }
        catch (Exception exception)
        {
            if (args.Contains("--headless", StringComparer.Ordinal) ||
                args.Contains("--ui-smoke", StringComparer.Ordinal) ||
                args.Contains("--maintain-logs", StringComparer.Ordinal))
            {
                Console.Error.WriteLine(exception);
                return 1;
            }

            MessageBox.Show(exception.ToString(), $"{ReleaseIdentity.DisplayName} - 起動失敗",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string? ReadString(IReadOnlyList<string> args, string key)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index] == key)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static int? ReadInt(IReadOnlyList<string> args, string key)
    {
        var value = ReadString(args, key);
        return value is null ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long? ReadLong(IReadOnlyList<string> args, string key)
    {
        var value = ReadString(args, key);
        return value is null ? null : long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
