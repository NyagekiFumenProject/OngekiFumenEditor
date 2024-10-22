using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Utils
{
    internal static class IPCHelper
    {
        const int FileSize = 10240;
        private static MemoryMappedFile mmf;
        private static bool enableMultiProc;
        private static readonly int currentPid;

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
                //there are other editors registered
                if (pid != 0)
                {
                    //check if host editor is dead or not 
                    var process = Process.GetProcessById(pid);
                    if (process is not null)
                    {
                        //check if have to wait for host editor
                        if (isWaitForPrev)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        //send args to host editor and later will process them
                        var r = "CMD:" + JsonSerializer.Serialize(new ArgsWrapper() { Args = args });

                        WriteLine(r, default);

                        Environment.Exit(0);
                        return;
                    }
                    break;
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
                var size = accessor.ReadInt32(sizeof(int));
                //check if readable
                if (size > 0)
                {
                    var bytes = new byte[size];
                    accessor.ReadArray(sizeof(int) * 2, bytes, 0, size);
                    accessor.Write(sizeof(int), 0); //set 0, notify others mmf is writable.

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
                var size = accessor.ReadInt32(sizeof(int));
                //check if writable 
                if (size > 0)
                {
                    Thread.Sleep(0);
                    continue;
                }

                var buffer = Encoding.UTF8.GetBytes(content);
                accessor.WriteArray(sizeof(int) * 2, buffer, 0, Math.Min(buffer.Length, FileSize - sizeof(int) * 2));
                accessor.Write(sizeof(int), buffer.Length); //notify others mmf is readable.
                accessor.Flush();

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
            return Process.GetProcessById(pid) is not null;
        }

        public static void SetSelfHost()
        {
            mmf = MemoryMappedFile.CreateOrOpen("OngekiFumenEditor_MMF", FileSize, MemoryMappedFileAccess.ReadWrite);
            using var accessor = mmf.CreateViewAccessor(0, FileSize);
            accessor.Write(0, currentPid);
        }
    }
}
