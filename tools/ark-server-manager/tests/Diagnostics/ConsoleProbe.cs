using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class ConsoleProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; public Coord(short x, short y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public short L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public Coord Size, Cursor; public ushort Attr; public Rect Window; public Coord Max; }
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(uint id);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool ReadConsoleOutputCharacter(IntPtr h, StringBuilder text, int count, Coord at, out int read);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleScreenBufferInfo(IntPtr h, out Info info);

    private static void Main(string[] args)
    {
        string result = "";
        try
        {
            uint pid = UInt32.Parse(args[0]);
            FreeConsole();
            if (!AttachConsole(pid)) throw new Exception("AttachConsole failed: " + Marshal.GetLastWin32Error());
            IntPtr output = GetStdHandle(-11);
            Info info;
            if (!GetConsoleScreenBufferInfo(output, out info)) throw new Exception("GetConsoleScreenBufferInfo failed");
            int width = Math.Max(1, (int)info.Size.X);
            int end = Math.Max(0, (int)info.Cursor.Y);
            int start = Math.Max(0, end - 700);
            int count = Math.Min(1000000, width * (end - start + 1));
            StringBuilder raw = new StringBuilder(count);
            int read;
            if (!ReadConsoleOutputCharacter(output, raw, count, new Coord(0, (short)start), out read)) throw new Exception("ReadConsoleOutputCharacter failed");
            StringBuilder lines = new StringBuilder();
            string value = raw.ToString(0, Math.Min(read, raw.Length));
            for (int offset = 0; offset < value.Length; offset += width)
            {
                string line = value.Substring(offset, Math.Min(width, value.Length - offset)).TrimEnd('\0', ' ');
                if (line.Length > 0) lines.AppendLine(line);
            }
            result = lines.ToString();
        }
        catch (Exception ex) { result = "ERROR=" + ex.Message; }
        finally { FreeConsole(); }
        File.WriteAllText(args[1], result, new UTF8Encoding(false));
    }
}
