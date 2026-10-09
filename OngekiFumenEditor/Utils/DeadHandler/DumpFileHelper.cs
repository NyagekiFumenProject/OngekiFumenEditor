using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace OngekiFumenEditor.Utils.DeadHandler
{
    public static class DumpFileHelper
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct MINIDUMP_EXCEPTION_INFORMATION
        {
            public uint ThreadId;

            public IntPtr ExceptionPointers;

            [MarshalAs(UnmanagedType.Bool)]
            public bool ClientPointers;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int UnhandledExceptionFilter(IntPtr exceptionInfo);

        // SetUnhandledExceptionFilter stores the callback beyond this call, so keep the
        // delegate rooted for the lifetime of the process.
        private static readonly UnhandledExceptionFilter unhandledExceptionFilter = OnWriteMiniDump;

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern UnhandledExceptionFilter SetUnhandledExceptionFilter([MarshalAs(UnmanagedType.FunctionPtr)] UnhandledExceptionFilter lpTopLevelExceptionFilter);

        [DllImport("dbghelp.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeHandle hFile, uint DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern uint GetCurrentThreadId();

        [DllImport("Kernel32.dll")]
        public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);

        public static void Init()
        {
            Directory.CreateDirectory(AppDirectoryHelper.ResolveRelative(ProgramSetting.Default.DumpFileDirPath));
            SetUnhandledExceptionFilter(unhandledExceptionFilter);
        }

        /// <summary>
        /// Writes a minidump. A zero <paramref name="exceptionInfo"/> is expected for
        /// managed exceptions and produces a dump without native exception context.
        /// </summary>
        public static string WriteMiniDump(IntPtr exceptionInfo)
        {
            var dumpDir = AppDirectoryHelper.ResolveRelative(ProgramSetting.Default.DumpFileDirPath);
            Directory.CreateDirectory(dumpDir);
            var filePath = Path.GetFullPath(Path.Combine(dumpDir, FileHelper.FilterFileName(DateTime.Now.ToString() + ".dmp")));

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            var currentProcess = Process.GetCurrentProcess();
            // MiniDumpWithFullMemory = 0x00000002
            // MiniDumpNormal = 0x00000000
            var dumpType = ProgramSetting.Default.IsFullDump ? 0x2 : 0x0;
            var exceptionParam = IntPtr.Zero;

            try
            {
                // Native crashes provide EXCEPTION_POINTERS. A managed exception does not,
                // so pass a null MINIDUMP_EXCEPTION_INFORMATION pointer in that case.
                if (exceptionInfo != IntPtr.Zero)
                {
                    exceptionParam = Marshal.AllocHGlobal(Marshal.SizeOf<MINIDUMP_EXCEPTION_INFORMATION>());
                    Marshal.StructureToPtr(
                        new MINIDUMP_EXCEPTION_INFORMATION
                        {
                            ThreadId = GetCurrentThreadId(),
                            ClientPointers = false,
                            ExceptionPointers = exceptionInfo
                        },
                        exceptionParam,
                        fDeleteOld: false);
                }

                var isSuccessful = MiniDumpWriteDump(currentProcess.Handle, (uint)currentProcess.Id, fileStream.SafeFileHandle, (uint)dumpType, exceptionParam, IntPtr.Zero, IntPtr.Zero);

                string getErrMsg()
                {
                    var code = Marshal.GetLastWin32Error();
                    if (code == 0)
                        return string.Empty;
                    IntPtr tempptr = IntPtr.Zero;
                    string msg = default;
                    FormatMessage(0x1300, ref tempptr, code, 0, ref msg, 255, ref tempptr);
                    return msg;
                }

                Log.LogError($"call MiniDumpWriteDump() exceptionInfo = {exceptionInfo} , dumpType = {dumpType} , isSuccessful = {isSuccessful} , getLastError = {(isSuccessful ? string.Empty : getErrMsg())} , dumpFilePath = {filePath}");
            }
            finally
            {
                if (exceptionParam != IntPtr.Zero)
                    Marshal.FreeHGlobal(exceptionParam);
            }

            try
            {
                // The dump result is logged through Log's queue. Waiting only on the
                // file sink could return before that record reaches the sink.
                Log.WaitForAllLogWriteDone().GetAwaiter().GetResult();
            }
            catch (Exception logException)
            {
                Debug.WriteLine($"Flush dump log failed : {logException}");
                FileLogOutput.WaitForWriteDone();
            }
            return filePath;
        }

        private static int OnWriteMiniDump(IntPtr exceptionInfo)
        {
            WriteMiniDump(exceptionInfo);
            return 1;
        }
    }
}
