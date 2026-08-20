using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ArkServerManager
{
    internal static class AttachCheckProgram
    {
        private static int Main(string[] args)
        {
            string outputPath = args.Length > 0 ? args[0] : "ResourceProbe-attach-check.txt";
            StringBuilder report = new StringBuilder();
            try
            {
                AppSettings settings = AppSettings.Load();
                using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                {
                    rcon.Connect();
                    string diagnostics = rcon.Command("ResourceProbe.Diagnostics", 700);
                    Require(diagnostics, @"(?m)^PLUGIN_VERSION=1\.2\s*$", "v1.2 diagnostics");
                    Require(diagnostics, @"(?m)^SCANNING_ENABLED=1\s*$", "startup scanning state");
                    report.AppendLine("DIAGNOSTICS_OK=1");
                    report.AppendLine(diagnostics.Trim());

                    string pause = rcon.Command("ResourceProbe.Pause", 400);
                    Require(pause, @"(?m)^SCANNING_ENABLED=0\s*$", "pause");
                    report.AppendLine("PAUSE_OK=1");

                    string resume = rcon.Command("ResourceProbe.Resume", 400);
                    Require(resume, @"(?m)^SCANNING_ENABLED=1\s*$", "resume");
                    report.AppendLine("RESUME_OK=1");

                    string scan = "";
                    DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                    while (DateTime.UtcNow < deadline)
                    {
                        scan = rcon.Command("ResourceProbe.Scan", 700);
                        if (Regex.IsMatch(scan, @"(?m)^READY=1\s*$")) break;
                        Thread.Sleep(2000);
                    }
                    Require(scan, @"(?m)^READY=1\s*$", "completed scan");
                    int zones = Regex.Matches(scan, @"(?m)^ZONE=").Count;
                    if (zones != 9) throw new InvalidOperationException("Expected 9 spots, received " + zones.ToString(CultureInfo.InvariantCulture));
                    report.AppendLine("SCAN_READY=1");
                    report.AppendLine("ZONE_COUNT=" + zones.ToString(CultureInfo.InvariantCulture));
                    report.AppendLine(scan.Trim());
                    report.AppendLine("RESULT=PASS");
                }
                return 0;
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT=FAIL");
                report.AppendLine("ERROR=" + error);
                return 1;
            }
            finally
            {
                report.AppendLine("CHECKED_AT=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
                Console.WriteLine(report.ToString());
            }
        }

        private static void Require(string value, string pattern, string name)
        {
            if (!Regex.IsMatch(value ?? "", pattern)) throw new InvalidOperationException("ResourceProbe " + name + " check failed. Response: " + value);
        }
    }
}
