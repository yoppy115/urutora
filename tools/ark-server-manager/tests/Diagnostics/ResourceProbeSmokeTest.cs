using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ArkServerManager
{
    internal static class SmokeTestProgram
    {
        private static int Main(string[] args)
        {
            string outputPath = args.Length > 0 ? args[0] : "ResourceProbe-runtime-test.txt";
            StringBuilder report = new StringBuilder();
            AppSettings settings = AppSettings.Load();
            report.AppendLine("ResourceProbe runtime test");
            report.AppendLine("Started=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("RCON=127.0.0.1:" + settings.RconPort.ToString(CultureInfo.InvariantCulture));
            bool connected = false;
            bool pluginPassed = false;
            Process server = null;
            try
            {
                if (Process.GetProcessesByName("ShooterGameServer").Length != 0)
                    throw new InvalidOperationException("ShooterGameServer is already running; refusing to create another process.");
                ProcessStartInfo start = new ProcessStartInfo(settings.ExecutablePath, BuildArguments(settings));
                start.WorkingDirectory = Path.GetDirectoryName(settings.ExecutablePath);
                start.UseShellExecute = true;
                server = Process.Start(start);
                report.AppendLine("SERVER_STARTED_PID=" + server.Id.ToString(CultureInfo.InvariantCulture));

                DateTime deadline = DateTime.UtcNow.AddMinutes(15);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                        {
                            rcon.Connect();
                            connected = true;
                            report.AppendLine("RCON_CONNECTED=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                            string diagnostics = rcon.Command("ResourceProbe.Diagnostics", 700);
                            report.AppendLine("DIAGNOSTICS_BEGIN");
                            report.AppendLine(diagnostics.Trim());
                            report.AppendLine("DIAGNOSTICS_END");
                            if (!Regex.IsMatch(diagnostics, @"(?m)^PLUGIN_VERSION=1\.2\s*$"))
                                throw new InvalidOperationException("ResourceProbe v1.2 diagnostics response was not received.");
                            if (!Regex.IsMatch(diagnostics, @"(?m)^SCANNING_ENABLED=1\s*$"))
                                throw new InvalidOperationException("Resource scanning was not enabled at startup.");

                            string pause = rcon.Command("ResourceProbe.Pause", 400);
                            if (!Regex.IsMatch(pause, @"(?m)^SCANNING_ENABLED=0\s*$"))
                                throw new InvalidOperationException("Pause command failed.");
                            report.AppendLine("PAUSE_OK=1");

                            string resume = rcon.Command("ResourceProbe.Resume", 400);
                            if (!Regex.IsMatch(resume, @"(?m)^SCANNING_ENABLED=1\s*$"))
                                throw new InvalidOperationException("Resume command failed.");
                            report.AppendLine("RESUME_OK=1");

                            string scan = "";
                            DateTime scanDeadline = DateTime.UtcNow.AddSeconds(25);
                            while (DateTime.UtcNow < scanDeadline)
                            {
                                scan = rcon.Command("ResourceProbe.Scan", 700);
                                if (Regex.IsMatch(scan, @"(?m)^READY=1\s*$")) break;
                                Thread.Sleep(2000);
                            }
                            if (!Regex.IsMatch(scan, @"(?m)^READY=1\s*$"))
                                throw new InvalidOperationException("A complete resource scan was not ready within 25 seconds.");
                            int zoneCount = Regex.Matches(scan, @"(?m)^ZONE=").Count;
                            if (zoneCount != 9) throw new InvalidOperationException("Expected 9 zone results, received " + zoneCount + ".");
                            report.AppendLine("SCAN_READY=1");
                            report.AppendLine("ZONE_COUNT=" + zoneCount.ToString(CultureInfo.InvariantCulture));
                            report.AppendLine("SCAN_RESULT_BEGIN");
                            report.AppendLine(scan.Trim());
                            report.AppendLine("SCAN_RESULT_END");
                            pluginPassed = true;

                            report.AppendLine("SAVEWORLD_SENT=1");
                            rcon.Command("SaveWorld", 500);
                            Thread.Sleep(3500);
                            report.AppendLine("DOEXIT_SENT=1");
                            rcon.Command("DoExit", 300);
                            break;
                        }
                    }
                    catch (Exception error)
                    {
                        if (connected) throw;
                        if (server.HasExited) throw new InvalidOperationException("ShooterGameServer exited before RCON became available. ExitCode=" + server.ExitCode, error);
                        report.AppendLine("WAITING=" + error.Message);
                        Thread.Sleep(5000);
                    }
                }
                if (!connected) throw new TimeoutException("RCON did not become available within 15 minutes.");
                report.AppendLine("RESULT=" + (pluginPassed ? "PASS" : "FAIL"));
                return pluginPassed ? 0 : 2;
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT=FAIL");
                report.AppendLine("ERROR=" + error);
                if (connected)
                {
                    try
                    {
                        using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                        {
                            rcon.Connect(); rcon.Command("SaveWorld", 500); Thread.Sleep(3500); rcon.Command("DoExit", 300);
                            report.AppendLine("FAILSAFE_STOP_SENT=1");
                        }
                    }
                    catch (Exception stopError) { report.AppendLine("FAILSAFE_STOP_ERROR=" + stopError.Message); }
                }
                else if (server != null && !server.HasExited)
                {
                    try { server.Kill(); server.WaitForExit(10000); report.AppendLine("PRE_READY_PROCESS_KILLED=1"); }
                    catch (Exception killError) { report.AppendLine("PRE_READY_KILL_ERROR=" + killError.Message); }
                }
                return 1;
            }
            finally
            {
                if (server != null && pluginPassed)
                {
                    try
                    {
                        if (!server.WaitForExit(60000)) report.AppendLine("SERVER_EXIT_TIMEOUT=1");
                        else report.AppendLine("SERVER_EXIT_CODE=" + server.ExitCode.ToString(CultureInfo.InvariantCulture));
                    }
                    catch (Exception waitError) { report.AppendLine("SERVER_EXIT_WAIT_ERROR=" + waitError.Message); }
                }
                report.AppendLine("Finished=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
                Console.WriteLine(report.ToString());
            }
        }

        private static string BuildArguments(AppSettings settings)
        {
            List<string> options = new List<string>();
            options.Add("SessionName=" + Clean(settings.SessionName));
            if (!String.IsNullOrEmpty(settings.ServerPassword)) options.Add("ServerPassword=" + Clean(settings.ServerPassword));
            if (!String.IsNullOrEmpty(settings.AdminPassword)) options.Add("ServerAdminPassword=" + Clean(settings.AdminPassword));
            options.Add("MaxPlayers=" + settings.MaxPlayers.ToString(CultureInfo.InvariantCulture));
            options.Add("Port=" + settings.GamePort.ToString(CultureInfo.InvariantCulture));
            options.Add("QueryPort=" + settings.QueryPort.ToString(CultureInfo.InvariantCulture));
            options.Add("RCONEnabled=True");
            options.Add("RCONPort=" + settings.RconPort.ToString(CultureInfo.InvariantCulture));
            options.Add("ServerPVE=" + Bool(settings.ServerPVE));
            options.Add("bUseSingleplayerSettings=" + Bool(settings.UseSingleplayerSettings));
            options.Add("DifficultyOffset=" + Float(settings.DifficultyOffset));
            if (settings.OverrideOfficialDifficulty > 0) options.Add("OverrideOfficialDifficulty=" + Float(settings.OverrideOfficialDifficulty));
            options.Add("XPMultiplier=" + Float(settings.XPMultiplier));
            options.Add("TamingSpeedMultiplier=" + Float(settings.TamingSpeedMultiplier));
            options.Add("HarvestAmountMultiplier=" + Float(settings.HarvestAmountMultiplier));
            options.Add("ResourcesRespawnPeriodMultiplier=" + Float(settings.ResourcesRespawnPeriodMultiplier));
            options.Add("DinoCountMultiplier=" + Float(settings.DinoCountMultiplier));
            options.Add("DayCycleSpeedScale=" + Float(settings.DayCycleSpeedScale));
            options.Add("DayTimeSpeedScale=" + Float(settings.DayTimeSpeedScale));
            options.Add("NightTimeSpeedScale=" + Float(settings.NightTimeSpeedScale));
            options.Add("MatingIntervalMultiplier=" + Float(settings.MatingIntervalMultiplier));
            options.Add("EggHatchSpeedMultiplier=" + Float(settings.EggHatchSpeedMultiplier));
            options.Add("BabyMatureSpeedMultiplier=" + Float(settings.BabyMatureSpeedMultiplier));
            options.Add("AllowThirdPersonPlayer=" + Bool(settings.AllowThirdPersonPlayer));
            options.Add("ServerCrosshair=" + Bool(settings.ServerCrosshair));
            options.Add("ShowMapPlayerLocation=" + Bool(settings.ShowMapPlayerLocation));
            options.Add("AllowFlyerCarryPvE=" + Bool(settings.AllowFlyerCarryPVE));
            options.Add("bDisableStructurePlacementCollision=" + Bool(settings.DisableStructurePlacementCollision));
            options.Add("ResourceNoReplenishRadiusStructures=" + Float(settings.ResourceNoReplenishRadiusStructures));
            options.Add("listen");
            string url = settings.MapName + "?" + String.Join("?", options.ToArray());
            return "\"" + url.Replace("\"", "") + "\" " + settings.AdditionalArguments;
        }

        private static string Bool(bool value) { return value ? "True" : "False"; }
        private static string Float(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string Clean(string value) { return (value ?? "").Replace("?", "").Replace("\"", "").Replace("\r", " ").Replace("\n", " "); }
    }
}
