using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class ServerConsoleTail
{
    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X; public short Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public short Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public Coord Size, Cursor; public ushort Attributes; public Rect Window; public Coord Max; }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int handle);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetConsoleScreenBufferInfo(IntPtr output, out Info info);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool ReadConsoleOutputCharacter(IntPtr output, StringBuilder text, int length, Coord origin, out int read);

    private static int Main(string[] args)
    {
        uint pid;
        if (args.Length == 0 || !UInt32.TryParse(args[0], out pid)) return 2;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            FreeConsole();
            if (!AttachConsole(pid)) { Thread.Sleep(200); continue; }
            try
            {
                IntPtr output = GetStdHandle(-11); Info info;
                if (!GetConsoleScreenBufferInfo(output, out info)) return 3;
                int width = Math.Max(1, (int)info.Size.X), end = Math.Max(0, (int)info.Cursor.Y), start = Math.Max(0, end - 500);
                int count = Math.Min(1000000, width * (end - start + 1));
                StringBuilder raw = new StringBuilder(count); int read;
                if (!ReadConsoleOutputCharacter(output, raw, count, new Coord { X=0, Y=(short)start }, out read)) return 4;
                for (int offset=0; offset<read; offset+=width)
                {
                    int length=Math.Min(width, read-offset);
                    string line=raw.ToString(offset,length).TrimEnd('\0',' ');
                    if (line.Length>0) Console.WriteLine(line);
                }
                return 0;
            }
            finally { FreeConsole(); }
        }
        return 5;
    }
}
