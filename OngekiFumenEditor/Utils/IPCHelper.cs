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

            var pid = accessor.ReadInt32(0);
            if (pid != 0)
            {
                var process = Process.GetProcessById(pid);
                if (process is not null)
                {
                    //send to host
                    var r = "CMD:" + JsonSerializer.Serialize(new ArgsWrapper() { Args = args });

                    var buffer = Encoding.UTF8.GetBytes(r);
                    accessor.WriteArray(sizeof(int) * 2, buffer, 0, Math.Min(buffer.Length, FileSize - sizeof(int) * 2));
                    accessor.Write(sizeof(int), buffer.Length);
                    accessor.Flush();

                    Environment.Exit(0);
                    return;
                }
            }

            accessor.Write(0, Process.GetCurrentProcess().Id);
        }

        public static string ReadLineAsync(CancellationToken cancellation)
        {
            if (enableMultiProc)
                return string.Empty;

            using var accessor = mmf.CreateViewAccessor(0, FileSize);

            while (!cancellation.IsCancellationRequested)
            {
                var size = accessor.ReadInt32(sizeof(int));
                if (size > 0)
                {
                    var bytes = new byte[size];
                    accessor.ReadArray(sizeof(int) * 2, bytes, 0, size);
                    accessor.Write(sizeof(int), 0);

                    return Encoding.UTF8.GetString(bytes);
                }
            }

            return string.Empty;
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
