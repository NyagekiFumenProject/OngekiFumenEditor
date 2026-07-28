using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;

namespace OngekiFumenEditor.Avalonia.Utils;

internal static class IPCHelper
{
    private const int FileSize = 10240;
    private static MemoryMappedFile mmf;
    private static readonly bool enableMultiProc;
    private static readonly int currentPid;
    private static readonly Mutex mutex = new(false, "OngekiFumenEditor_Mutex");
    private static readonly EventWaitHandle ReadEvent = new(false, EventResetMode.AutoReset, "OngekiFumenEditor_ReadEvent");

    internal class ArgsWrapper
    {
        public string[] Args { get; set; }
    }

    static IPCHelper()
    {
        enableMultiProc = Properties.ProgramSetting.Default.EnableMultiInstances;
        currentPid = Process.GetCurrentProcess().Id;
    }

    public static void Init(string[] args)
    {
        if (enableMultiProc)
            return;

        mmf = MemoryMappedFile.CreateOrOpen("OngekiFumenEditor_MMF", FileSize, MemoryMappedFileAccess.ReadWrite);
        using var accessor = mmf.CreateViewAccessor(0, FileSize);

        var isWaitForPrev = args.Contains("--wait", StringComparer.InvariantCultureIgnoreCase);

        while (true)
        {
            var pid = accessor.ReadInt32(0);
            if (pid != 0)
            {
                try
                {
                    _ = Process.GetProcessById(pid);
                }
                catch
                {
                    break;
                }

                if (isWaitForPrev)
                {
                    Thread.Sleep(100);
                    continue;
                }

                var r = "CMD:" + JsonSerializer.Serialize(new ArgsWrapper { Args = args });
                WriteLine(r, default);
                Environment.Exit(0);
                return;
            }

            accessor.Write(0, Process.GetCurrentProcess().Id);
            return;
        }
    }

    public static string ReadLine(CancellationToken cancellation)
    {
        if (enableMultiProc)
            return string.Empty;

        using var accessor = mmf.CreateViewAccessor(0, FileSize);

        while (!cancellation.IsCancellationRequested)
        {
            while (true)
            {
                if (ReadEvent.WaitOne(2000))
                    break;

                if (cancellation.IsCancellationRequested)
                    return string.Empty;
            }

            var size = accessor.ReadInt32(sizeof(int));
            if (size > 0)
            {
                mutex.WaitOne();
                var bytes = new byte[size];
                accessor.ReadArray(sizeof(int) * 2, bytes, 0, size);
                accessor.Write(sizeof(int), 0);
                mutex.ReleaseMutex();
                return Encoding.UTF8.GetString(bytes);
            }

            Thread.Sleep(10);
        }

        return string.Empty;
    }

    public static void WriteLine(string content, CancellationToken cancellation)
    {
        using var accessor = mmf.CreateViewAccessor(0, FileSize);

        while (!cancellation.IsCancellationRequested)
        {
            mutex.WaitOne();
            var size = accessor.ReadInt32(sizeof(int));
            if (size > 0)
            {
                mutex.ReleaseMutex();
                Thread.Sleep(0);
                continue;
            }

            var buffer = Encoding.UTF8.GetBytes(content);
            accessor.WriteArray(sizeof(int) * 2, buffer, 0, Math.Min(buffer.Length, FileSize - sizeof(int) * 2));
            accessor.Write(sizeof(int), buffer.Length);
            accessor.Flush();
            mutex.ReleaseMutex();
            ReadEvent.Set();
            break;
        }
    }

    public static bool IsSelfHost()
    {
        mmf = MemoryMappedFile.CreateOrOpen("OngekiFumenEditor_MMF", FileSize, MemoryMappedFileAccess.ReadWrite);
        using var accessor = mmf.CreateViewAccessor(0, FileSize);
        var pid = accessor.ReadInt32(0);
        return pid == currentPid;
    }

    public static bool IsHostAlive()
    {
        mmf = MemoryMappedFile.CreateOrOpen("OngekiFumenEditor_MMF", FileSize, MemoryMappedFileAccess.ReadWrite);
        using var accessor = mmf.CreateViewAccessor(0, FileSize);
        var pid = accessor.ReadInt32(0);
        if (pid == 0)
            return false;
        try
        {
            _ = Process.GetProcessById(pid);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetSelfHost()
    {
        mmf = MemoryMappedFile.CreateOrOpen("OngekiFumenEditor_MMF", FileSize, MemoryMappedFileAccess.ReadWrite);
        using var accessor = mmf.CreateViewAccessor(0, FileSize);
        accessor.Write(0, currentPid);
    }
}
