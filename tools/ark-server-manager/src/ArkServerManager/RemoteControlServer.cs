using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ArkServerManager
{
    internal sealed class RemoteState
    {
        public bool Running;
        public bool Starting;
        public bool Stopping;
        public bool CanOperate;
        public string Status = "";
        public string Detail = "";
        public string Uptime = "—";
        public string Cpu = "—";
        public string Memory = "—";
        public string Port = "—";
        public string Map = "";
        public string Session = "";
        public string Log = "";
        public string Schedule = "";
    }

    internal sealed class RemoteScheduleState
    {
        public bool OneTimeStartEnabled;
        public string OneTimeStartAt = "";
        public bool OneTimeStopEnabled;
        public string OneTimeStopAt = "";
        public bool DailyStartEnabled;
        public string DailyStartTime = "08:00";
        public bool DailyStopEnabled;
        public string DailyStopTime = "23:00";
        public string Summary = "予約なし";
    }

    internal sealed class RemoteControlServer : IDisposable
    {
        private readonly int port;
        private readonly string pin;
        private readonly IPAddress tailnetAddress;
        private readonly Func<RemoteState> getState;
        private readonly Func<string> startServer;
        private readonly Func<string> stopServer;
        private readonly Func<string, string> sendCommand;
        private readonly Func<string, string, string> searchDino;
        private readonly Func<RemoteScheduleState> getSchedule;
        private readonly Func<RemoteScheduleState, string> saveSchedule;
        private readonly Func<string> clearSchedule;
        private readonly object sessionLock = new object();
        private readonly object listenerLock = new object();
        private readonly Dictionary<string, DateTime> sessions = new Dictionary<string, DateTime>();
        private readonly List<TcpListener> listeners = new List<TcpListener>();
        private readonly List<Thread> listenerThreads = new List<Thread>();
        private volatile bool stopping;
        private DateTime nextTailnetRetryAt = DateTime.MinValue;
        private int failedLogins;
        private DateTime loginLockedUntil = DateTime.MinValue;

        public bool TailnetListening { get; private set; }

        public RemoteControlServer(int port, string pin, IPAddress tailnetAddress, Func<RemoteState> getState,
            Func<string> startServer, Func<string> stopServer,
            Func<string, string> sendCommand, Func<string, string, string> searchDino,
            Func<RemoteScheduleState> getSchedule, Func<RemoteScheduleState, string> saveSchedule, Func<string> clearSchedule)
        {
            this.port = port;
            this.pin = pin ?? "";
            this.tailnetAddress = tailnetAddress;
            this.getState = getState;
            this.startServer = startServer;
            this.stopServer = stopServer;
            this.sendCommand = sendCommand;
            this.searchDino = searchDino;
            this.getSchedule = getSchedule;
            this.saveSchedule = saveSchedule;
            this.clearSchedule = clearSchedule;
        }

        public void Start()
        {
            if (listeners.Count > 0) return;
            stopping = false;
            lock (listenerLock) StartListener(IPAddress.Loopback, "ARK Remote Control (local)");
            TailnetListening = false;
            TryStartTailnetListener(true);
        }

        public bool RetryTailnetListener()
        {
            return TryStartTailnetListener(false);
        }

        private bool TryStartTailnetListener(bool ignoreRetryDelay)
        {
            if (stopping || TailnetListening || tailnetAddress == null || IPAddress.IsLoopback(tailnetAddress)) return false;
            lock (listenerLock)
            {
                if (stopping || TailnetListening) return false;
                DateTime now = DateTime.UtcNow;
                if (!ignoreRetryDelay && now < nextTailnetRetryAt) return false;
                nextTailnetRetryAt = now.AddSeconds(2);
                try
                {
                    StartListener(tailnetAddress, "ARK Remote Control (tailnet)");
                    TailnetListening = true;
                    return true;
                }
                catch
                {
                    TailnetListening = false;
                    return false;
                }
            }
        }

        public void Stop()
        {
            stopping = true;
            TcpListener[] listenersToStop;
            Thread[] threadsToJoin;
            lock (listenerLock)
            {
                listenersToStop = listeners.ToArray();
                threadsToJoin = listenerThreads.ToArray();
                listeners.Clear();
                listenerThreads.Clear();
                TailnetListening = false;
            }
            foreach (TcpListener current in listenersToStop) try { current.Stop(); } catch { }
            foreach (Thread thread in threadsToJoin) if (thread != null && thread.IsAlive) thread.Join(1500);
        }

        public void Dispose() { Stop(); }

        private void StartListener(IPAddress address, string threadName)
        {
            TcpListener source = new TcpListener(address, port);
            source.Start(20);
            listeners.Add(source);
            Thread thread = new Thread(new ThreadStart(delegate { ListenLoop(source); })) { IsBackground = true, Name = threadName };
            listenerThreads.Add(thread);
            thread.Start();
        }

        private void ListenLoop(TcpListener source)
        {
            while (!stopping)
            {
                try
                {
                    TcpClient client = source.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch (SocketException) { if (!stopping) Thread.Sleep(100); }
                catch (ObjectDisposedException) { break; }
                catch { if (!stopping) Thread.Sleep(100); }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 15000;
                    client.SendTimeout = 15000;
                    using (NetworkStream stream = client.GetStream())
                    {
                        HttpRequest request = ReadRequest(stream);
                        if (request == null) return;
                        Route(stream, request);
                    }
                }
                catch { }
            }
        }

        private void Route(NetworkStream stream, HttpRequest request)
        {
            string path = request.Path;
            if (request.Method == "GET" && path == "/")
            {
                WriteResponse(stream, 200, "text/html; charset=utf-8", MobilePage, null);
                return;
            }
            if (request.Method == "GET" && path == "/favicon.ico")
            {
                WriteResponse(stream, 204, "text/plain", "", null);
                return;
            }
            if (request.Method == "GET" && path == "/fjordur-map.png")
            {
                using (Stream source = typeof(RemoteControlServer).Assembly.GetManifestResourceStream("ArkServerManager.FjordurMap.png"))
                {
                    if (source == null) { WriteResponse(stream, 404, "text/plain", "", null); return; }
                    byte[] data = new byte[source.Length];
                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int read = source.Read(data, offset, data.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }
                    WriteBinaryResponse(stream, 200, "image/png", data);
                }
                return;
            }
            if (request.Method == "POST" && path == "/api/login")
            {
                HandleLogin(stream, request);
                return;
            }
            if (!IsAuthorized(request))
            {
                WriteJson(stream, 401, false, "PINを入力してください。", null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/logout")
            {
                RemoveSession(request);
                string logoutSecure = IsHttpsRequest(request) ? "; Secure" : "";
                WriteJson(stream, 200, true, "ログアウトしました。", null,
                    "Set-Cookie: ark_session=; Path=/; Max-Age=0; HttpOnly" + logoutSecure + "; SameSite=Strict\r\n");
                return;
            }
            if (request.Method == "GET" && path == "/api/status")
            {
                RemoteState state = getState();
                string extra = "\"running\":" + Bool(state.Running) +
                    ",\"starting\":" + Bool(state.Starting) +
                    ",\"stopping\":" + Bool(state.Stopping) +
                    ",\"canOperate\":" + Bool(state.CanOperate) +
                    ",\"status\":\"" + JsonEscape(state.Status) + "\"" +
                    ",\"detail\":\"" + JsonEscape(state.Detail) + "\"" +
                    ",\"uptime\":\"" + JsonEscape(state.Uptime) + "\"" +
                    ",\"cpu\":\"" + JsonEscape(state.Cpu) + "\"" +
                    ",\"memory\":\"" + JsonEscape(state.Memory) + "\"" +
                    ",\"port\":\"" + JsonEscape(state.Port) + "\"" +
                    ",\"map\":\"" + JsonEscape(state.Map) + "\"" +
                    ",\"session\":\"" + JsonEscape(state.Session) + "\"" +
                    ",\"schedule\":\"" + JsonEscape(state.Schedule) + "\"" +
                    ",\"log\":\"" + JsonEscape(state.Log) + "\"";
                WriteJson(stream, 200, true, "", extra, null);
                return;
            }
            if (request.Method == "GET" && path == "/api/schedule")
            {
                RemoteScheduleState state = getSchedule();
                string extra = "\"oneTimeStartEnabled\":" + Bool(state.OneTimeStartEnabled) +
                    ",\"oneTimeStartAt\":\"" + JsonEscape(state.OneTimeStartAt) + "\"" +
                    ",\"oneTimeStopEnabled\":" + Bool(state.OneTimeStopEnabled) +
                    ",\"oneTimeStopAt\":\"" + JsonEscape(state.OneTimeStopAt) + "\"" +
                    ",\"dailyStartEnabled\":" + Bool(state.DailyStartEnabled) +
                    ",\"dailyStartTime\":\"" + JsonEscape(state.DailyStartTime) + "\"" +
                    ",\"dailyStopEnabled\":" + Bool(state.DailyStopEnabled) +
                    ",\"dailyStopTime\":\"" + JsonEscape(state.DailyStopTime) + "\"" +
                    ",\"summary\":\"" + JsonEscape(state.Summary) + "\"";
                WriteJson(stream, 200, true, "", extra, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/schedule")
            {
                Dictionary<string, string> form = ParseForm(request.Body);
                RemoteScheduleState state = new RemoteScheduleState
                {
                    OneTimeStartEnabled = FormBool(form, "oneTimeStartEnabled"),
                    OneTimeStartAt = FormValue(form, "oneTimeStartAt"),
                    OneTimeStopEnabled = FormBool(form, "oneTimeStopEnabled"),
                    OneTimeStopAt = FormValue(form, "oneTimeStopAt"),
                    DailyStartEnabled = FormBool(form, "dailyStartEnabled"),
                    DailyStartTime = FormValue(form, "dailyStartTime"),
                    DailyStopEnabled = FormBool(form, "dailyStopEnabled"),
                    DailyStopTime = FormValue(form, "dailyStopTime")
                };
                string message = saveSchedule(state);
                bool ok = !message.StartsWith("エラー:", StringComparison.Ordinal);
                WriteJson(stream, ok ? 200 : 400, ok, message, null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/schedule/clear")
            {
                string message = clearSchedule();
                bool ok = !message.StartsWith("エラー:", StringComparison.Ordinal);
                WriteJson(stream, ok ? 200 : 400, ok, message, null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/start")
            {
                WriteJson(stream, 200, true, startServer(), null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/stop")
            {
                WriteJson(stream, 200, true, stopServer(), null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/command")
            {
                Dictionary<string, string> form = ParseForm(request.Body);
                string command;
                form.TryGetValue("command", out command);
                if (String.IsNullOrWhiteSpace(command))
                {
                    WriteJson(stream, 400, false, "コマンドを入力してください。", null, null);
                    return;
                }
                WriteJson(stream, 200, true, sendCommand(command.Trim()), null, null);
                return;
            }
            if (request.Method == "POST" && path == "/api/dino")
            {
                Dictionary<string, string> form = ParseForm(request.Body);
                string name; string category;
                form.TryGetValue("name", out name); form.TryGetValue("category", out category);
                if (String.IsNullOrWhiteSpace(name))
                {
                    WriteJson(stream, 400, false, "恐竜名を入力してください。", null, null);
                    return;
                }
                WriteJson(stream, 200, true, searchDino(name.Trim(), category ?? "all"), null, null);
                return;
            }
            WriteJson(stream, 404, false, "ページが見つかりません。", null, null);
        }

        private void HandleLogin(NetworkStream stream, HttpRequest request)
        {
            lock (sessionLock)
            {
                if (DateTime.UtcNow < loginLockedUntil)
                {
                    WriteJson(stream, 429, false, "PINの入力回数が多すぎます。1分後にお試しください。", null, null);
                    return;
                }
            }
            Dictionary<string, string> form = ParseForm(request.Body);
            string entered;
            form.TryGetValue("pin", out entered);
            if (!FixedTimeEquals(pin, entered ?? ""))
            {
                lock (sessionLock)
                {
                    failedLogins++;
                    if (failedLogins >= 5) { failedLogins = 0; loginLockedUntil = DateTime.UtcNow.AddMinutes(1); }
                }
                Thread.Sleep(350);
                WriteJson(stream, 401, false, "PINが違います。", null, null);
                return;
            }
            string token = CreateToken();
            lock (sessionLock)
            {
                failedLogins = 0; loginLockedUntil = DateTime.MinValue;
                sessions[token] = DateTime.UtcNow.AddHours(12);
                RemoveExpiredSessions();
            }
            string secure = IsHttpsRequest(request) ? "; Secure" : "";
            string cookie = "Set-Cookie: ark_session=" + token + "; Path=/; Max-Age=43200; HttpOnly" + secure + "; SameSite=Strict\r\n";
            WriteJson(stream, 200, true, "ログインしました。", null, cookie);
        }

        private static bool IsHttpsRequest(HttpRequest request)
        {
            string protocol;
            return request.Headers.TryGetValue("x-forwarded-proto", out protocol) && protocol.Equals("https", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAuthorized(HttpRequest request)
        {
            string token = GetSessionToken(request);
            if (token.Length == 0) return false;
            lock (sessionLock)
            {
                DateTime expiry;
                if (!sessions.TryGetValue(token, out expiry) || expiry <= DateTime.UtcNow)
                {
                    sessions.Remove(token); return false;
                }
                sessions[token] = DateTime.UtcNow.AddHours(12);
                return true;
            }
        }

        private void RemoveSession(HttpRequest request)
        {
            string token = GetSessionToken(request);
            if (token.Length == 0) return;
            lock (sessionLock) sessions.Remove(token);
        }

        private static string GetSessionToken(HttpRequest request)
        {
            string cookie;
            if (!request.Headers.TryGetValue("cookie", out cookie)) return "";
            foreach (string part in cookie.Split(';'))
            {
                string item = part.Trim();
                if (item.StartsWith("ark_session=", StringComparison.Ordinal)) return item.Substring(12);
            }
            return "";
        }

        private void RemoveExpiredSessions()
        {
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, DateTime> item in sessions)
                if (item.Value <= DateTime.UtcNow) expired.Add(item.Key);
            foreach (string key in expired) sessions.Remove(key);
        }

        private static string CreateToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            byte[] a = Encoding.UTF8.GetBytes(expected ?? "");
            byte[] b = Encoding.UTF8.GetBytes(actual ?? "");
            int diff = a.Length ^ b.Length;
            int length = Math.Max(a.Length, b.Length);
            for (int i = 0; i < length; i++)
                diff |= (i < a.Length ? a[i] : 0) ^ (i < b.Length ? b[i] : 0);
            return diff == 0;
        }

        private static HttpRequest ReadRequest(NetworkStream stream)
        {
            MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[4096];
            int headerEnd = -1;
            while (buffer.Length < 65536 && headerEnd < 0)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read <= 0) return null;
                buffer.Write(chunk, 0, read);
                byte[] current = buffer.ToArray();
                headerEnd = FindHeaderEnd(current);
            }
            if (headerEnd < 0) return null;
            byte[] data = buffer.ToArray();
            string headerText = Encoding.ASCII.GetString(data, 0, headerEnd);
            string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return null;
            HttpRequest request = new HttpRequest { Method = first[0].ToUpperInvariant(), Path = first[1].Split('?')[0] };
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                request.Headers[lines[i].Substring(0, colon).Trim().ToLowerInvariant()] = lines[i].Substring(colon + 1).Trim();
            }
            int contentLength = 0; string value;
            if (request.Headers.TryGetValue("content-length", out value)) Int32.TryParse(value, out contentLength);
            if (contentLength < 0 || contentLength > 65536) return null;
            int bodyOffset = headerEnd + 4;
            while (data.Length - bodyOffset < contentLength)
            {
                int read = stream.Read(chunk, 0, Math.Min(chunk.Length, contentLength - (data.Length - bodyOffset)));
                if (read <= 0) return null;
                buffer.Write(chunk, 0, read); data = buffer.ToArray();
            }
            request.Body = contentLength > 0 ? Encoding.UTF8.GetString(data, bodyOffset, contentLength) : "";
            return request;
        }

        private static int FindHeaderEnd(byte[] data)
        {
            for (int i = 0; i <= data.Length - 4; i++)
                if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10) return i;
            return -1;
        }

        private static Dictionary<string, string> ParseForm(string body)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in (body ?? "").Split('&'))
            {
                if (part.Length == 0) continue;
                int equals = part.IndexOf('=');
                string key = equals >= 0 ? part.Substring(0, equals) : part;
                string value = equals >= 0 ? part.Substring(equals + 1) : "";
                try
                {
                    key = Uri.UnescapeDataString(key.Replace('+', ' '));
                    value = Uri.UnescapeDataString(value.Replace('+', ' '));
                }
                catch { }
                result[key] = value;
            }
            return result;
        }

        private static string FormValue(Dictionary<string, string> form, string key)
        {
            string value;
            return form != null && form.TryGetValue(key, out value) ? value ?? "" : "";
        }

        private static bool FormBool(Dictionary<string, string> form, string key)
        {
            string value = FormValue(form, key);
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteJson(NetworkStream stream, int status, bool ok, string message, string extra, string extraHeaders)
        {
            string json = "{\"ok\":" + Bool(ok) + ",\"message\":\"" + JsonEscape(message ?? "") + "\"";
            if (!String.IsNullOrEmpty(extra)) json += "," + extra;
            json += "}";
            WriteResponse(stream, status, "application/json; charset=utf-8", json, extraHeaders);
        }

        private static void WriteResponse(NetworkStream stream, int status, string contentType, string body, string extraHeaders)
        {
            byte[] content = Encoding.UTF8.GetBytes(body ?? "");
            string reason = status == 200 ? "OK" : status == 204 ? "No Content" : status == 400 ? "Bad Request" : status == 401 ? "Unauthorized" : status == 404 ? "Not Found" : status == 429 ? "Too Many Requests" : "Error";
            StringBuilder header = new StringBuilder();
            header.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            header.Append("Content-Type: ").Append(contentType).Append("\r\n");
            header.Append("Content-Length: ").Append(content.Length).Append("\r\n");
            header.Append("Connection: close\r\nCache-Control: no-store\r\n");
            header.Append("X-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nReferrer-Policy: no-referrer\r\n");
            header.Append("Content-Security-Policy: default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'\r\n");
            if (!String.IsNullOrEmpty(extraHeaders)) header.Append(extraHeaders);
            header.Append("\r\n");
            byte[] headers = Encoding.ASCII.GetBytes(header.ToString());
            stream.Write(headers, 0, headers.Length);
            if (content.Length > 0) stream.Write(content, 0, content.Length);
            stream.Flush();
        }

        private static void WriteBinaryResponse(NetworkStream stream, int status, string contentType, byte[] content)
        {
            content = content ?? new byte[0];
            string reason = status == 200 ? "OK" : "Error";
            StringBuilder header = new StringBuilder();
            header.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            header.Append("Content-Type: ").Append(contentType).Append("\r\n");
            header.Append("Content-Length: ").Append(content.Length).Append("\r\n");
            header.Append("Cache-Control: public, max-age=86400\r\nConnection: close\r\n\r\n");
            byte[] headers = Encoding.ASCII.GetBytes(header.ToString());
            stream.Write(headers, 0, headers.Length);
            if (content.Length > 0) stream.Write(content, 0, content.Length);
            stream.Flush();
        }

        private static string Bool(bool value) { return value ? "true" : "false"; }

        private static string JsonEscape(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            StringBuilder output = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': output.Append("\\\\"); break;
                    case '"': output.Append("\\\""); break;
                    case '\r': output.Append("\\r"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\t': output.Append("\\t"); break;
                    default: if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4")); else output.Append(c); break;
                }
            }
            return output.ToString();
        }

        private sealed class HttpRequest
        {
            public string Method = "";
            public string Path = "";
            public string Body = "";
            public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly string MobilePage = MobilePageTemplate.Replace(
            "{{DINO_OPTIONS}}", BuildDinoOptions());

        private static string BuildDinoOptions()
        {
            StringBuilder html = new StringBuilder();
            foreach (DinoCatalogEntry entry in FjordurDinoCatalog.GetJapaneseOrderedEntries())
                html.Append("<option value='").Append(WebUtility.HtmlEncode(entry.Name)).Append("'>");
            return html.ToString();
        }

        private const string MobilePageTemplate = @"<!doctype html>
<html lang='ja'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1,viewport-fit=cover'>
<title>ARK Server Manager</title><style>
:root{color-scheme:dark;--bg:#0b1016;--card:#1d2732;--line:#344454;--text:#edf2f6;--muted:#9ba8b6;--green:#37c47d;--red:#ef5d66;--blue:#3d8ceb;--amber:#f2b046}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:-apple-system,BlinkMacSystemFont,'Yu Gothic UI',sans-serif;padding:env(safe-area-inset-top) 14px env(safe-area-inset-bottom);min-height:100vh}
.wrap{max-width:720px;margin:auto}.top{display:flex;justify-content:space-between;align-items:center;padding:18px 4px 12px}.brand{font-weight:800;letter-spacing:.08em}.muted{color:var(--muted);font-size:13px}.card{background:var(--card);border:1px solid #263440;border-radius:18px;padding:18px;margin:10px 0;box-shadow:0 8px 28px #0004}
h1,h2,p{margin-top:0}h1{font-size:25px;margin-bottom:5px}h2{font-size:17px;margin-bottom:14px}.state{display:flex;align-items:center;gap:11px}.dot{width:13px;height:13px;border-radius:50%;background:var(--muted);box-shadow:0 0 14px currentColor}.dot.green{background:var(--green);color:var(--green)}.dot.amber{background:var(--amber);color:var(--amber)}
 .grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.stat{background:#121a22;border-radius:13px;padding:12px}.stat b{display:block;font-size:18px;margin-top:4px}.actions{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:15px}.schedule-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}.schedule-item{background:#121a22;border:1px solid #2a3947;border-radius:13px;padding:12px}.schedule-check{display:flex;align-items:center;gap:8px;font-weight:750;margin-bottom:9px}.schedule-check input{width:21px;height:21px;margin:0;flex:0 0 auto}.date-time-row{display:grid;grid-template-columns:minmax(0,1.45fr) minmax(0,1fr);gap:8px}.schedule-buttons{display:grid;grid-template-columns:1fr 1fr;gap:9px;margin-top:12px}.schedule-summary{white-space:pre-line;background:#0b1117;border-radius:12px;padding:12px;margin-bottom:12px;color:#cbd6df}.mapcanvas{width:100%;height:auto;background:#0b1016;border:1px solid var(--line);border-radius:12px;margin-top:10px}.dinolist{max-height:250px;overflow:auto;margin-top:10px;display:grid;gap:7px}.dinochoice{display:grid;grid-template-columns:26px 1fr;align-items:center;gap:8px;padding:10px;border:1px solid var(--line);border-radius:11px;background:#111a22}.dinochoice input{width:20px;height:20px;margin:0}.dinochoice.selected{border-color:#7162dc;background:#252449}.dinochoice.active{outline:3px solid #fff8;box-shadow:0 0 0 2px #7162dc}.dinochoice .badge{font-weight:800;color:#fff}
button,input,select,textarea{width:100%;font:inherit;border-radius:12px;border:1px solid var(--line);background:#111a22;color:var(--text);padding:13px}button{border:0;font-weight:750;cursor:pointer;background:var(--blue)}button.green{background:var(--green);color:#06140d}button.red{background:var(--red)}button.ghost{background:#283642}button:disabled{opacity:.45}textarea{min-height:78px;resize:vertical}.row{display:grid;grid-template-columns:1fr 130px;gap:9px}.result,pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#0b1117;border-radius:12px;padding:13px;max-height:320px;overflow:auto;font:13px ui-monospace,SFMono-Regular,Menlo,monospace}.hidden{display:none!important}.login{max-width:420px;margin:14vh auto 0}.error{color:#ff949b;min-height:20px;margin-top:9px}.ok{color:#78e0a8}.tiny{font-size:12px;color:var(--muted)}
 @media(max-width:480px){body{padding-left:10px;padding-right:10px}.card{border-radius:15px;padding:15px}.row,.schedule-grid{grid-template-columns:1fr}.actions{grid-template-columns:1fr 1fr}}
</style></head><body><div class='wrap'>
<section id='login' class='card login'><h1>ARK Server Manager</h1><p class='muted'>スマホ操作用PINを入力してください</p><input id='pin' type='password' inputmode='numeric' maxlength='6' autocomplete='one-time-code' placeholder='6桁のPIN'><button id='loginBtn' class='green' style='margin-top:12px'>ログイン</button><div id='loginError' class='error'></div></section>
<main id='app' class='hidden'><div class='top'><div><div class='brand'>ARK SERVER MANAGER</div><div id='serverName' class='muted'></div></div><button id='logoutBtn' class='ghost' style='width:auto;padding:9px 12px'>ログアウト</button></div>
<section class='card'><div class='state'><span id='dot' class='dot'></span><div><h1 id='status'>確認中</h1><div id='detail' class='muted'></div></div></div><div class='actions'><button id='startBtn' class='green'>サーバーを起動</button><button id='stopBtn' class='red'>安全に停止</button></div><div id='actionMessage' class='tiny' style='margin-top:10px'></div></section>
 <section class='grid'><div class='stat'><span class='muted'>稼働時間</span><b id='uptime'>—</b></div><div class='stat'><span class='muted'>CPU</span><b id='cpu'>—</b></div><div class='stat'><span class='muted'>メモリ</span><b id='memory'>—</b></div><div class='stat'><span class='muted'>ポート</span><b id='port'>—</b></div></section>
 <section class='card'><h2>日時設定</h2><div id='scheduleSummary' class='schedule-summary'>予約を読み込み中…</div><div class='schedule-grid'>
 <div class='schedule-item'><label class='schedule-check'><input id='oneStartEnabled' type='checkbox'>1回だけ起動</label><div class='date-time-row'><input id='oneStartDate' type='date' aria-label='起動日'><input id='oneStartTime' type='time' aria-label='起動時刻'></div></div>
 <div class='schedule-item'><label class='schedule-check'><input id='oneStopEnabled' type='checkbox'>1回だけ停止</label><div class='date-time-row'><input id='oneStopDate' type='date' aria-label='停止日'><input id='oneStopTime' type='time' aria-label='停止時刻'></div></div>
 <div class='schedule-item'><label class='schedule-check'><input id='dailyStartEnabled' type='checkbox'>毎日定時に起動</label><input id='dailyStartTime' type='time' value='08:00'></div>
 <div class='schedule-item'><label class='schedule-check'><input id='dailyStopEnabled' type='checkbox'>毎日定時に停止</label><input id='dailyStopTime' type='time' value='23:00'></div></div>
 <div class='schedule-buttons'><button id='scheduleSaveBtn'>日時設定を保存</button><button id='scheduleClearBtn' class='ghost'>すべて解除</button></div><div id='scheduleMessage' class='tiny' style='margin-top:10px'></div></section>
<section class='card'><h2>ゲームへのコマンド</h2><textarea id='command' placeholder='例: ListPlayers / SaveWorld / Broadcast メッセージ'></textarea><button id='commandBtn' style='margin-top:9px'>送信</button><pre id='commandResult' class='result'>結果がここに表示されます。</pre></section>
<section class='card'><h2>恐竜検索</h2><div class='row'><input id='dinoName' list='dinos' placeholder='日本語名またはクラス名'><select id='category'><option value='all'>すべて</option><option value='wild'>野生のみ</option><option value='tamed'>テイム済みのみ</option></select></div><datalist id='dinos'>{{DINO_OPTIONS}}</datalist><button id='dinoBtn' style='margin-top:9px'>数と場所を検索</button><pre id='dinoResult' class='result'>レベルが高い順に表示します。座標はGPS緯度・経度＋Zで表示します。</pre><div id='dinoMapTools' class='hidden'><div class='tiny'>マップへ表示する個体を最大5体選択してください。白い枠が現在選択中です。</div><div id='dinoLocationList' class='dinolist'></div><button id='showDinoMapBtn' style='margin-top:9px' disabled>選択中をマップ表示 (0/5)</button><canvas id='dinoMap' class='mapcanvas' width='640' height='500'></canvas><div id='dinoMapInfo' class='tiny'></div></div></section>
<section class='card'><h2>サーバーログ</h2><pre id='log'>ログを取得中…</pre></section></main></div>
<script>
const q=id=>document.getElementById(id);let refreshing=false;
async function call(path,method='GET',data=null){const opt={method,credentials:'same-origin',headers:{}};if(data){opt.headers['Content-Type']='application/x-www-form-urlencoded;charset=UTF-8';opt.body=new URLSearchParams(data).toString()}const r=await fetch(path,opt);let j={ok:false,message:'通信エラー'};try{j=await r.json()}catch(e){}if(r.status===401&&path!='/api/login'){showLogin()}return {status:r.status,data:j}}
function showLogin(){q('login').classList.remove('hidden');q('app').classList.add('hidden')}
function showApp(){q('login').classList.add('hidden');q('app').classList.remove('hidden')}
 async function login(){q('loginError').textContent='';const r=await call('/api/login','POST',{pin:q('pin').value});if(r.data.ok){q('pin').value='';showApp();refresh();loadSchedule()}else q('loginError').textContent=r.data.message}
 async function refresh(){if(refreshing)return;refreshing=true;try{const r=await call('/api/status');if(!r.data.ok)return;showApp();const s=r.data;q('serverName').textContent=s.map+' • '+s.session;q('status').textContent=s.status;q('detail').textContent=s.detail;q('uptime').textContent=s.uptime;q('cpu').textContent=s.cpu;q('memory').textContent=s.memory;q('port').textContent=s.port;q('scheduleSummary').textContent=s.schedule||'予約なし';q('log').textContent=s.log||'ログはまだありません。';q('dot').className='dot '+(s.running?(s.starting||s.stopping?'amber':'green'):'');q('startBtn').disabled=s.running||s.stopping;q('stopBtn').disabled=!s.running||s.stopping;q('commandBtn').disabled=!s.canOperate;q('dinoBtn').disabled=!s.canOperate}catch(e){}finally{refreshing=false}}
 const splitDateTime=value=>{const parts=String(value||'').split('T');return {date:parts[0]||'',time:parts[1]||''}},joinDateTime=(date,time)=>date&&time?date+'T'+time:'';
 async function loadSchedule(){const r=await call('/api/schedule');if(!r.data.ok)return;const s=r.data,start=splitDateTime(s.oneTimeStartAt),stop=splitDateTime(s.oneTimeStopAt);q('oneStartEnabled').checked=!!s.oneTimeStartEnabled;q('oneStartDate').value=start.date;q('oneStartTime').value=start.time;q('oneStopEnabled').checked=!!s.oneTimeStopEnabled;q('oneStopDate').value=stop.date;q('oneStopTime').value=stop.time;q('dailyStartEnabled').checked=!!s.dailyStartEnabled;q('dailyStartTime').value=s.dailyStartTime||'08:00';q('dailyStopEnabled').checked=!!s.dailyStopEnabled;q('dailyStopTime').value=s.dailyStopTime||'23:00';q('scheduleSummary').textContent=s.summary||'予約なし'}
 async function saveScheduleSettings(){q('scheduleMessage').textContent='保存中…';const r=await call('/api/schedule','POST',{oneTimeStartEnabled:q('oneStartEnabled').checked,oneTimeStartAt:joinDateTime(q('oneStartDate').value,q('oneStartTime').value),oneTimeStopEnabled:q('oneStopEnabled').checked,oneTimeStopAt:joinDateTime(q('oneStopDate').value,q('oneStopTime').value),dailyStartEnabled:q('dailyStartEnabled').checked,dailyStartTime:q('dailyStartTime').value,dailyStopEnabled:q('dailyStopEnabled').checked,dailyStopTime:q('dailyStopTime').value});q('scheduleMessage').textContent=r.data.message;if(r.data.ok){await loadSchedule();await refresh()}}
 async function clearScheduleSettings(){if(!confirm('単発・毎日の日時設定をすべて解除しますか？'))return;q('scheduleMessage').textContent='解除中…';const r=await call('/api/schedule/clear','POST');q('scheduleMessage').textContent=r.data.message;if(r.data.ok){await loadSchedule();await refresh()}}
async function action(path,confirmText){if(confirmText&&!confirm(confirmText))return;q('actionMessage').textContent='処理中…';const r=await call(path,'POST');q('actionMessage').textContent=r.data.message;setTimeout(refresh,600)}
 q('loginBtn').onclick=login;q('pin').onkeydown=e=>{if(e.key==='Enter')login()};q('logoutBtn').onclick=async()=>{await call('/api/logout','POST');showLogin()};q('startBtn').onclick=()=>action('/api/start','サーバーを起動しますか？');q('stopBtn').onclick=()=>action('/api/stop','SaveWorld後に安全に停止しますか？');q('scheduleSaveBtn').onclick=saveScheduleSettings;q('scheduleClearBtn').onclick=clearScheduleSettings;
q('commandBtn').onclick=async()=>{const c=q('command').value.trim();if(!c)return;q('commandResult').textContent='送信中…';const r=await call('/api/command','POST',{command:c});q('commandResult').textContent=r.data.message};
let dinoLocations=[],selectedDinoIndices=[],activeDinoIndex=-1;const pinColors=['#ff4a52','#37c47d','#4e97ff','#f2b046','#be69ee'],mapLeft=105,mapTop=35,mapSize=410,lonGrid=[42,159,276,392,508,622,739,853,971,1107,1243],latGrid=[27,139,251,363,476,589,702,815,927,1040,1153];
const gridPixel=(v,a,total)=>{v=Math.max(0,Math.min(100,v));const i=Math.min(9,Math.floor(v/10)),f=(v-i*10)/10;return (a[i]+(a[i+1]-a[i])*f)/total},mapX=v=>mapLeft+gridPixel(v,lonGrid,1266)*mapSize,mapY=v=>mapTop+gridPixel(v,latGrid,1243)*mapSize;
const fjordurMapImage=new Image();fjordurMapImage.src='/fjordur-map.png';fjordurMapImage.onload=()=>{if(selectedDinoIndices.length)drawDinoMap()};
 function renderDinoChoices(){const box=q('dinoLocationList');box.innerHTML='';dinoLocations.forEach((p,i)=>{const selectedAt=selectedDinoIndices.indexOf(i),row=document.createElement('div'),cb=document.createElement('input'),text=document.createElement('div');row.className='dinochoice'+(selectedAt>=0?' selected':'')+(activeDinoIndex===i?' active':'');cb.type='checkbox';cb.checked=selectedAt>=0;cb.onchange=()=>toggleDinoChoice(i,cb.checked);text.innerHTML=(selectedAt>=0?'<span class=""badge"">#'+(selectedAt+1)+'</span>  ':'')+'Lv.'+p.level+(p.dead?' <span class=""badge"">［死］</span>':'')+' '+p.area+'<br><span class=""tiny"">緯度 '+p.lat.toFixed(2)+' / 経度 '+p.lon.toFixed(2)+' / Z '+p.z.toFixed(1)+'</span>';text.onclick=()=>{if(selectedDinoIndices.includes(i)){activeDinoIndex=i;renderDinoChoices();drawDinoMap()}};row.appendChild(cb);row.appendChild(text);box.appendChild(row)});const b=q('showDinoMapBtn');b.disabled=!selectedDinoIndices.length;b.textContent='選択中をマップ表示 ('+selectedDinoIndices.length+'/5)'}
function toggleDinoChoice(i,checked){const found=selectedDinoIndices.indexOf(i);if(checked&&found<0){if(selectedDinoIndices.length>=5){alert('マップへ表示できるのは最大5体です。');renderDinoChoices();return}selectedDinoIndices.push(i);activeDinoIndex=i}else if(!checked&&found>=0){selectedDinoIndices.splice(found,1);if(activeDinoIndex===i)activeDinoIndex=selectedDinoIndices.length?selectedDinoIndices[0]:-1}renderDinoChoices();if(selectedDinoIndices.length)drawDinoMap();else{const c=q('dinoMap');c.getContext('2d').clearRect(0,0,c.width,c.height);q('dinoMapInfo').textContent=''}}
 function parseDinoLocations(text){dinoLocations=[];selectedDinoIndices=[];activeDinoIndex=-1;for(const line of String(text||'').split(/\r?\n/)){const m=line.match(/Lv\.(\d+)(\s*［死］)?.*?エリア=(.*?)\s{2,}緯度=(-?[\d.]+)\s+経度=(-?[\d.]+)\s+Z=(-?[\d.]+)/);if(m)dinoLocations.push({level:+m[1],dead:!!m[2],area:m[3],lat:+m[4],lon:+m[5],z:+m[6]})}renderDinoChoices();q('dinoMapTools').classList.toggle('hidden',!dinoLocations.length)}
function drawDinoMap(){if(!selectedDinoIndices.length)return;if(!selectedDinoIndices.includes(activeDinoIndex))activeDinoIndex=selectedDinoIndices[0];const c=q('dinoMap'),g=c.getContext('2d');g.clearRect(0,0,c.width,c.height);g.fillStyle='#143240';g.fillRect(mapLeft,mapTop,mapSize,mapSize);if(fjordurMapImage.complete)g.drawImage(fjordurMapImage,mapLeft,mapTop,mapSize,mapSize);g.strokeStyle='#82becd';g.lineWidth=1;g.strokeRect(mapLeft,mapTop,mapSize,mapSize);const drawPin=(i,active)=>{const p=dinoLocations[i],order=selectedDinoIndices.indexOf(i),px=mapX(p.lon),py=mapY(p.lat),r=active?13:9;if(active){g.fillStyle=pinColors[order]+'66';g.beginPath();g.arc(px,py,25,0,Math.PI*2);g.fill()}g.fillStyle=pinColors[order];g.beginPath();g.arc(px,py,r,0,Math.PI*2);g.fill();g.strokeStyle='white';g.lineWidth=active?3:1.5;g.stroke();g.fillStyle='white';g.font='bold '+(active?13:10)+'px sans-serif';g.textAlign='center';g.textBaseline='middle';g.fillText(String(order+1),px,py);if(active){g.textAlign='left';g.textBaseline='alphabetic';g.font='bold 14px sans-serif';g.fillText('選択中 #'+(order+1)+'  Lv.'+p.level,px+17,py-15)}};selectedDinoIndices.filter(i=>i!==activeDinoIndex).forEach(i=>drawPin(i,false));drawPin(activeDinoIndex,true);const p=dinoLocations[activeDinoIndex],order=selectedDinoIndices.indexOf(activeDinoIndex);q('dinoMapInfo').textContent='選択中 #'+(order+1)+'/'+selectedDinoIndices.length+'  '+p.area+' / 緯度 '+p.lat.toFixed(2)+' / 経度 '+p.lon.toFixed(2)+' / Z '+p.z.toFixed(1)}
q('dinoMap').onclick=e=>{if(!selectedDinoIndices.length)return;const c=q('dinoMap'),r=c.getBoundingClientRect(),mx=(e.clientX-r.left)*c.width/r.width,my=(e.clientY-r.top)*c.height/r.height;let nearest=-1,best=30*30;selectedDinoIndices.forEach(i=>{const p=dinoLocations[i],dx=mx-mapX(p.lon),dy=my-mapY(p.lat),d=dx*dx+dy*dy;if(d<best){best=d;nearest=i}});if(nearest>=0){activeDinoIndex=nearest;renderDinoChoices();drawDinoMap()}};
q('showDinoMapBtn').onclick=drawDinoMap;
q('dinoBtn').onclick=async()=>{const n=q('dinoName').value.trim();if(!n)return;q('dinoResult').textContent='保存データを検索中…';q('dinoMapTools').classList.add('hidden');const r=await call('/api/dino','POST',{name:n,category:q('category').value});q('dinoResult').textContent=r.data.message;parseDinoLocations(r.data.message)};
 refresh();loadSchedule();setInterval(refresh,3000);
</script></body></html>";
    }
}
