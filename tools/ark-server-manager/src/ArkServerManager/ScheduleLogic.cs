using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArkServerManager
{
    internal enum ScheduledAction
    {
        None,
        Start,
        Stop
    }

    internal sealed class ScheduleDecision
    {
        public ScheduledAction Action = ScheduledAction.None;
        public bool IsDaily;
        public DateTime DueAt = DateTime.MinValue;

        public bool HasAction { get { return Action != ScheduledAction.None; } }
    }

    internal static class ScheduleLogic
    {
        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm";
        private const string TimeFormat = @"hh\:mm";

        public static DateTime ToMinute(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Local);
        }

        public static TimeSpan ToTime(DateTime value)
        {
            return new TimeSpan(value.Hour, value.Minute, 0);
        }

        public static DateTime CombineDateAndTime(DateTime date, DateTime time)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Local);
        }

        public static DateTime MostRecentOccurrence(TimeSpan time, DateTime now)
        {
            DateTime today = now.Date.Add(time);
            return today <= now ? today : today.AddDays(-1);
        }

        public static RemoteScheduleState ToRemoteState(AppSettings settings, DateTime now, string lastAction)
        {
            return new RemoteScheduleState
            {
                OneTimeStartEnabled = settings.ScheduledStartEnabled,
                OneTimeStartAt = settings.ScheduledStartAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                OneTimeStopEnabled = settings.ScheduledStopEnabled,
                OneTimeStopAt = settings.ScheduledStopAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                DailyStartEnabled = settings.DailyStartEnabled,
                DailyStartTime = settings.DailyStartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
                DailyStopEnabled = settings.DailyStopEnabled,
                DailyStopTime = settings.DailyStopTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
                Summary = Summary(settings, now, lastAction)
            };
        }

        public static bool TryApply(AppSettings settings, RemoteScheduleState request, DateTime now, out string error)
        {
            error = "";
            if (request == null)
            {
                error = "予約内容がありません。";
                return false;
            }

            DateTime startAt = settings.ScheduledStartAt;
            DateTime stopAt = settings.ScheduledStopAt;
            TimeSpan dailyStart = settings.DailyStartTime;
            TimeSpan dailyStop = settings.DailyStopTime;

            if (request.OneTimeStartEnabled &&
                !DateTime.TryParseExact(request.OneTimeStartAt ?? "", DateTimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out startAt))
            {
                error = "単発の起動日時を正しく指定してください。";
                return false;
            }
            if (request.OneTimeStopEnabled &&
                !DateTime.TryParseExact(request.OneTimeStopAt ?? "", DateTimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out stopAt))
            {
                error = "単発の停止日時を正しく指定してください。";
                return false;
            }
            if (request.DailyStartEnabled &&
                !TimeSpan.TryParseExact(request.DailyStartTime ?? "", TimeFormat, CultureInfo.InvariantCulture, out dailyStart))
            {
                error = "毎日の起動時刻を正しく指定してください。";
                return false;
            }
            if (request.DailyStopEnabled &&
                !TimeSpan.TryParseExact(request.DailyStopTime ?? "", TimeFormat, CultureInfo.InvariantCulture, out dailyStop))
            {
                error = "毎日の停止時刻を正しく指定してください。";
                return false;
            }

            startAt = ToMinute(startAt);
            stopAt = ToMinute(stopAt);
            if (request.OneTimeStartEnabled && startAt < now.AddSeconds(-5))
            {
                error = "単発の起動予約には現在より後の日時を指定してください。";
                return false;
            }
            if (request.OneTimeStopEnabled && stopAt < now.AddSeconds(-5))
            {
                error = "単発の停止予約には現在より後の日時を指定してください。";
                return false;
            }

            bool dailyStartChanged = request.DailyStartEnabled &&
                (!settings.DailyStartEnabled || settings.DailyStartTime != dailyStart);
            bool dailyStopChanged = request.DailyStopEnabled &&
                (!settings.DailyStopEnabled || settings.DailyStopTime != dailyStop);

            settings.ScheduledStartEnabled = request.OneTimeStartEnabled;
            settings.ScheduledStartAt = startAt;
            settings.ScheduledStopEnabled = request.OneTimeStopEnabled;
            settings.ScheduledStopAt = stopAt;
            settings.DailyStartEnabled = request.DailyStartEnabled;
            settings.DailyStartTime = dailyStart;
            settings.DailyStopEnabled = request.DailyStopEnabled;
            settings.DailyStopTime = dailyStop;
            if (dailyStartChanged) settings.DailyStartLastRunAt = MostRecentOccurrence(dailyStart, now);
            if (dailyStopChanged) settings.DailyStopLastRunAt = MostRecentOccurrence(dailyStop, now);
            return true;
        }

        public static void Clear(AppSettings settings)
        {
            settings.ScheduledStartEnabled = false;
            settings.ScheduledStopEnabled = false;
            settings.DailyStartEnabled = false;
            settings.DailyStopEnabled = false;
        }

        public static bool HasActive(AppSettings settings)
        {
            return settings.ScheduledStartEnabled || settings.ScheduledStopEnabled ||
                settings.DailyStartEnabled || settings.DailyStopEnabled;
        }

        public static string Summary(AppSettings settings, DateTime now, string lastAction)
        {
            List<string> oneTime = new List<string>();
            List<string> daily = new List<string>();
            if (settings.ScheduledStartEnabled)
                oneTime.Add("単発起動 " + settings.ScheduledStartAt.ToString("MM/dd HH:mm") + "（" + Remaining(settings.ScheduledStartAt, now) + "）");
            if (settings.ScheduledStopEnabled)
                oneTime.Add("単発停止 " + settings.ScheduledStopAt.ToString("MM/dd HH:mm") + "（" + Remaining(settings.ScheduledStopAt, now) + "）");
            if (settings.DailyStartEnabled) daily.Add("毎日起動 " + settings.DailyStartTime.ToString(TimeFormat));
            if (settings.DailyStopEnabled) daily.Add("毎日停止 " + settings.DailyStopTime.ToString(TimeFormat));

            List<string> lines = new List<string>();
            if (oneTime.Count > 0) lines.Add(String.Join(" / ", oneTime.ToArray()));
            if (daily.Count > 0) lines.Add(String.Join(" / ", daily.ToArray()));
            if (lines.Count > 0) return String.Join("\r\n", lines.ToArray());
            return !String.IsNullOrEmpty(lastAction) ? lastAction : "予約なし";
        }

        public static ScheduleDecision ConsumeDue(AppSettings settings, DateTime now, bool deferStop)
        {
            bool oneStartDue = settings.ScheduledStartEnabled && settings.ScheduledStartAt <= now;
            bool oneStopDue = settings.ScheduledStopEnabled && settings.ScheduledStopAt <= now;
            DateTime dailyStartOccurrence = MostRecentOccurrence(settings.DailyStartTime, now);
            DateTime dailyStopOccurrence = MostRecentOccurrence(settings.DailyStopTime, now);
            bool dailyStartDue = settings.DailyStartEnabled && settings.DailyStartLastRunAt < dailyStartOccurrence;
            bool dailyStopDue = settings.DailyStopEnabled && settings.DailyStopLastRunAt < dailyStopOccurrence;

            ScheduleDecision decision = new ScheduleDecision();
            if (oneStartDue && settings.ScheduledStartAt > decision.DueAt)
            {
                decision.DueAt = settings.ScheduledStartAt;
                decision.Action = ScheduledAction.Start;
                decision.IsDaily = false;
            }
            if (oneStopDue && settings.ScheduledStopAt >= decision.DueAt)
            {
                decision.DueAt = settings.ScheduledStopAt;
                decision.Action = ScheduledAction.Stop;
                decision.IsDaily = false;
            }
            if (dailyStartDue && dailyStartOccurrence > decision.DueAt)
            {
                decision.DueAt = dailyStartOccurrence;
                decision.Action = ScheduledAction.Start;
                decision.IsDaily = true;
            }
            if (dailyStopDue && dailyStopOccurrence >= decision.DueAt)
            {
                decision.DueAt = dailyStopOccurrence;
                decision.Action = ScheduledAction.Stop;
                decision.IsDaily = true;
            }

            if (!decision.HasAction || (deferStop && decision.Action == ScheduledAction.Stop)) return decision;
            if (oneStartDue) settings.ScheduledStartEnabled = false;
            if (oneStopDue) settings.ScheduledStopEnabled = false;
            if (dailyStartDue) settings.DailyStartLastRunAt = dailyStartOccurrence;
            if (dailyStopDue) settings.DailyStopLastRunAt = dailyStopOccurrence;
            return decision;
        }

        private static string Remaining(DateTime at, DateTime now)
        {
            TimeSpan remaining = at - now;
            if (remaining.TotalSeconds <= 0) return "実行待ち";
            if (remaining.TotalDays >= 1) return ((int)remaining.TotalDays) + "日" + remaining.Hours + "時間後";
            if (remaining.TotalHours >= 1) return ((int)remaining.TotalHours) + "時間" + remaining.Minutes + "分後";
            return Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes)) + "分後";
        }
    }
}
