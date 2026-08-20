using System;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ArkServerManager
{
    internal static class ScheduleIntegrationTest
    {
        private static string Request(string url, string method, string body, CookieContainer cookies)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.CookieContainer = cookies;
            request.Timeout = 5000;
            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [STAThread]
        public static int Main()
        {
            string settingsDirectory = Path.Combine(Path.GetTempPath(), "ArkManagerScheduleTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(settingsDirectory);
            Environment.SetEnvironmentVariable("ARK_MANAGER_SETTINGS_DIR", settingsDirectory);
            try
            {
                AppSettings settings = AppSettings.Load();
                settings.ServerPassword = "";
                settings.AdminPassword = "";
                settings.RemotePin = "";
                settings.DailyStartEnabled = true;
                settings.DailyStartTime = new TimeSpan(7, 15, 0);
                settings.DailyStartLastRunAt = new DateTime(2026, 8, 11, 7, 15, 0, DateTimeKind.Local);
                settings.DailyStopEnabled = true;
                settings.DailyStopTime = new TimeSpan(23, 40, 0);
                settings.DailyStopLastRunAt = new DateTime(2026, 8, 11, 23, 40, 0, DateTimeKind.Local);
                settings.Save();
                AppSettings loaded = AppSettings.Load();
                Assert(loaded.DailyStartEnabled && loaded.DailyStartTime == new TimeSpan(7, 15, 0), "daily start did not round-trip");
                Assert(loaded.DailyStopEnabled && loaded.DailyStopTime == new TimeSpan(23, 40, 0), "daily stop did not round-trip");
                Assert(loaded.DailyStartLastRunAt == settings.DailyStartLastRunAt, "daily start last-run did not round-trip");
                Assert(loaded.DailyStopLastRunAt == settings.DailyStopLastRunAt, "daily stop last-run did not round-trip");

                TimeSpan alreadyPassedToday = DateTime.Now.TimeOfDay.Subtract(new TimeSpan(1, 0, 0));
                if (alreadyPassedToday < TimeSpan.Zero) alreadyPassedToday = new TimeSpan(23, 0, 0);
                alreadyPassedToday = new TimeSpan(alreadyPassedToday.Hours, alreadyPassedToday.Minutes, 0);
                RemoteScheduleState firstEnable = new RemoteScheduleState
                {
                    DailyStartEnabled = true,
                    DailyStartTime = alreadyPassedToday.ToString(@"hh\:mm"),
                    DailyStopEnabled = false,
                    DailyStopTime = "23:00"
                };
                string scheduleError;
                Assert(ScheduleLogic.TryApply(loaded, firstEnable, DateTime.Now, out scheduleError), "central schedule validation rejected a valid daily schedule");
                DateTime expectedOccurrence = ScheduleLogic.MostRecentOccurrence(alreadyPassedToday, DateTime.Now);
                Assert(loaded.DailyStartLastRunAt == expectedOccurrence, "first enable would incorrectly catch up immediately");
                DateTime beforeRepeat = loaded.DailyStartLastRunAt;
                Assert(ScheduleLogic.TryApply(loaded, firstEnable, DateTime.Now, out scheduleError), "unchanged daily schedule was rejected");
                Assert(loaded.DailyStartLastRunAt == beforeRepeat, "unchanged daily schedule lost its duplicate guard");
                RemoteScheduleState invalidPast = new RemoteScheduleState
                {
                    OneTimeStartEnabled = true,
                    OneTimeStartAt = "2000-01-01T00:00",
                    DailyStartTime = "08:00",
                    DailyStopTime = "23:00"
                };
                Assert(!ScheduleLogic.TryApply(loaded, invalidPast, DateTime.Now, out scheduleError), "past one-time schedule was accepted");

                DateTime crossMidnightNow = new DateTime(2026, 8, 12, 1, 0, 0, DateTimeKind.Local);
                Assert(ScheduleLogic.MostRecentOccurrence(new TimeSpan(23, 0, 0), crossMidnightNow) == new DateTime(2026, 8, 11, 23, 0, 0), "previous-day occurrence is wrong");
                Assert(ScheduleLogic.MostRecentOccurrence(new TimeSpan(0, 30, 0), crossMidnightNow) == new DateTime(2026, 8, 12, 0, 30, 0), "same-day occurrence is wrong");
                Assert(ScheduleLogic.CombineDateAndTime(new DateTime(2026, 8, 20), new DateTime(2000, 1, 1, 17, 45, 38)) ==
                    new DateTime(2026, 8, 20, 17, 45, 0, DateTimeKind.Local), "separate date/time inputs were not combined correctly");

                DinoCatalogEntry[] orderedDinos = FjordurDinoCatalog.GetJapaneseOrderedEntries();
                Assert(orderedDinos.Length == FjordurDinoCatalog.Entries.Length, "Japanese dino ordering lost catalog entries");
                CompareInfo japanese = CultureInfo.GetCultureInfo("ja-JP").CompareInfo;
                for (int i = 1; i < orderedDinos.Length; i++)
                    Assert(japanese.Compare(FjordurDinoCatalog.JapaneseSortKey(orderedDinos[i - 1].Name),
                        FjordurDinoCatalog.JapaneseSortKey(orderedDinos[i].Name),
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth) <= 0,
                        "dino catalog is not in Japanese order");

                DinoSaveSnapshot currentDinos = new DinoSaveSnapshot();
                currentDinos.Locations.Add(new DinoLocationRecord { DinoId = "10:20", IsWild = true, X = 100, Y = 200, Z = 300 });
                currentDinos.Locations.Add(new DinoLocationRecord { DinoId = "30:40", IsWild = false, X = 100, Y = 200, Z = 300 });
                var unchangedHistory = new System.Collections.Generic.List<DinoSaveSnapshot>();
                for (int i = 0; i < 4; i++)
                {
                    DinoSaveSnapshot snapshot = new DinoSaveSnapshot();
                    snapshot.Locations.Add(new DinoLocationRecord { DinoId = "10:20", IsWild = true, X = 100 + i, Y = 200, Z = 300 });
                    snapshot.Locations.Add(new DinoLocationRecord { DinoId = "30:40", IsWild = false, X = 100, Y = 200, Z = 300 });
                    unchangedHistory.Add(snapshot);
                }
                var stationaryIds = DinoHistoryLogic.FindStationaryWildDinoIds(currentDinos, unchangedHistory);
                Assert(stationaryIds.Contains("10:20") && !stationaryIds.Contains("30:40"), "stationary marker did not stay wild-only");
                unchangedHistory[3].Locations[0].X = 250;
                Assert(!DinoHistoryLogic.FindStationaryWildDinoIds(currentDinos, unchangedHistory).Contains("10:20"),
                    "a dino that moved by more than one meter was marked stationary");

                loaded.ScheduledStartEnabled = false;
                loaded.ScheduledStopEnabled = false;
                loaded.DailyStartEnabled = true;
                loaded.DailyStartTime = new TimeSpan(8, 0, 0);
                loaded.DailyStartLastRunAt = new DateTime(2026, 8, 11, 8, 0, 0);
                loaded.DailyStopEnabled = true;
                loaded.DailyStopTime = new TimeSpan(23, 0, 0);
                loaded.DailyStopLastRunAt = new DateTime(2026, 8, 10, 23, 0, 0);
                ScheduleDecision latestMissed = ScheduleLogic.ConsumeDue(loaded, new DateTime(2026, 8, 12, 1, 0, 0), false);
                Assert(latestMissed.Action == ScheduledAction.Stop && latestMissed.IsDaily, "latest missed daily state was not selected");
                ScheduleDecision duplicate = ScheduleLogic.ConsumeDue(loaded, new DateTime(2026, 8, 12, 1, 0, 0), false);
                Assert(!duplicate.HasAction, "daily action would execute twice after persistence");

                TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                RemoteScheduleState schedule = new RemoteScheduleState();
                RemoteControlServer server = new RemoteControlServer(port, "123456", null,
                    delegate { return new RemoteState { Status = "停止中", Schedule = schedule.Summary }; },
                    delegate { return "start"; }, delegate { return "stop"; },
                    delegate(string command) { return command; }, delegate(string name, string category) { return name; },
                    delegate { return schedule; },
                    delegate(RemoteScheduleState value) { schedule = value; schedule.Summary = "保存済み"; return "日時設定を保存しました。"; },
                    delegate { schedule = new RemoteScheduleState(); schedule.Summary = "予約なし"; return "解除しました。"; });
                server.Start();
                Thread.Sleep(100);
                string root = "http://127.0.0.1:" + port;
                CookieContainer cookies = new CookieContainer();
                string page = Request(root + "/", "GET", null, cookies);
                Assert(page.Contains("dailyStartTime") && page.Contains("scheduleSaveBtn") &&
                    page.Contains("oneStartDate") && page.Contains("oneStartTime") &&
                    page.Contains("oneStopDate") && page.Contains("oneStopTime") && page.Contains("［死］"), "mobile controls are missing");
                string login = Request(root + "/api/login", "POST", "pin=123456", cookies);
                Assert(login.Contains("\"ok\":true"), "login failed");
                string saved = Request(root + "/api/schedule", "POST",
                    "oneTimeStartEnabled=false&oneTimeStopEnabled=false&dailyStartEnabled=true&dailyStartTime=06%3A30&dailyStopEnabled=true&dailyStopTime=22%3A45", cookies);
                Assert(saved.Contains("\"ok\":true"), "schedule save API failed");
                string fetched = Request(root + "/api/schedule", "GET", null, cookies);
                Assert(fetched.Contains("\"dailyStartEnabled\":true") && fetched.Contains("\"dailyStartTime\":\"06:30\""), "schedule GET API mismatch");
                string cleared = Request(root + "/api/schedule/clear", "POST", "", cookies);
                Assert(cleared.Contains("\"ok\":true"), "schedule clear API failed");
                server.Dispose();

                IPAddress tailnetIp = IPAddress.Parse("100.100.176.17");
                TcpListener occupiedTailnetPort = new TcpListener(tailnetIp, 0);
                occupiedTailnetPort.Start();
                int retryPort = ((IPEndPoint)occupiedTailnetPort.LocalEndpoint).Port;
                RemoteControlServer retryServer = new RemoteControlServer(retryPort, "123456", tailnetIp,
                    delegate { return new RemoteState { Status = "停止中" }; },
                    delegate { return "start"; }, delegate { return "stop"; },
                    delegate(string command) { return command; }, delegate(string name, string category) { return name; },
                    delegate { return new RemoteScheduleState(); },
                    delegate(RemoteScheduleState value) { return "保存しました。"; },
                    delegate { return "解除しました。"; });
                retryServer.Start();
                Assert(!retryServer.TailnetListening, "tailnet listener unexpectedly started on an occupied address");
                occupiedTailnetPort.Stop();
                DateTime retryDeadline = DateTime.UtcNow.AddSeconds(4);
                while (!retryServer.TailnetListening && DateTime.UtcNow < retryDeadline)
                {
                    retryServer.RetryTailnetListener();
                    Thread.Sleep(100);
                }
                Assert(retryServer.TailnetListening, "tailnet listener did not recover after the address became available");
                string recoveredPage = Request("http://" + tailnetIp + ":" + retryPort + "/", "GET", null, new CookieContainer());
                Assert(recoveredPage.Contains("ARK Server Manager"), "recovered tailnet listener did not serve the mobile page");
                retryServer.Dispose();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (MainForm smokeForm = new MainForm())
                using (System.Windows.Forms.Timer closeTimer = new System.Windows.Forms.Timer())
                {
                    BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                    DateTimePicker startDateControl = (DateTimePicker)typeof(MainForm).GetField("scheduledStartPicker", privateInstance).GetValue(smokeForm);
                    DateTimePicker startTimeControl = (DateTimePicker)typeof(MainForm).GetField("scheduledStartTimePicker", privateInstance).GetValue(smokeForm);
                    CheckBox startEnabledControl = (CheckBox)typeof(MainForm).GetField("scheduledStartEnabledBox", privateInstance).GetValue(smokeForm);
                    Assert(startDateControl.CustomFormat == "yyyy/MM/dd" && !startDateControl.ShowUpDown, "PC start date control is not independently editable");
                    Assert(startTimeControl.CustomFormat == "HH:mm" && startTimeControl.ShowUpDown && startTimeControl.Enabled, "PC start time control is not independently editable");
                    startEnabledControl.Checked = true;
                    startDateControl.Value = DateTime.Today.AddDays(2);
                    startTimeControl.Value = DateTime.Today.AddHours(17).AddMinutes(45);
                    AppSettings formSettings = (AppSettings)typeof(MainForm).GetField("settings", privateInstance).GetValue(smokeForm);
                    // DPAPI is unavailable in the isolated test account, so keep encrypted fields empty for this save-path test.
                    formSettings.ServerPassword = "";
                    formSettings.AdminPassword = "";
                    formSettings.RemotePin = "";
                    typeof(MainForm).GetMethod("SaveSchedule", privateInstance).Invoke(smokeForm, new object[] { null, EventArgs.Empty });
                    Assert(formSettings.ScheduledStartAt.Date == DateTime.Today.AddDays(2) &&
                        formSettings.ScheduledStartAt.Hour == 17 && formSettings.ScheduledStartAt.Minute == 45,
                        "PC date/time controls did not save the selected time");
                    if (File.Exists(@"D:\arkserver\ShooterGame\Saved\SavedArks\Fjordur.ark") &&
                        File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ARK Dino Search.exe")))
                    {
                        object dinoResult = typeof(MainForm).GetMethod("ExecuteSavedDinoSearch", privateInstance)
                            .Invoke(smokeForm, new object[] { "LionfishLion_Character_BP_C", "wild" });
                        Type dinoResultType = dinoResult.GetType();
                        string dinoText = (string)dinoResultType.GetField("Text").GetValue(dinoResult);
                        var dinoLocations = (System.Collections.IList)dinoResultType.GetField("Locations").GetValue(dinoResult);
                        Assert(dinoText.Contains("［死］") && dinoLocations.Count > 0, "five-save dino history search did not complete");
                        Assert(!dinoLocations[0].ToString().Contains("DINOID=") && dinoLocations[0].ToString().Contains("緯度="),
                            "internal dino identity leaked into the visible result or GPS formatting was lost");
                    }
                    smokeForm.Opacity = 0;
                    smokeForm.ShowInTaskbar = false;
                    closeTimer.Interval = 250;
                    closeTimer.Tick += delegate { closeTimer.Stop(); smokeForm.Close(); };
                    closeTimer.Start();
                    Application.Run(smokeForm);
                    Assert(smokeForm.IsDisposed, "main window did not shut down cleanly");
                }
                Console.WriteLine("SCHEDULE_INTEGRATION_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
            finally
            {
                try { Directory.Delete(settingsDirectory, true); } catch { }
            }
        }
    }
}
