using Microsoft.Win32.SafeHandles;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    internal static partial class ConsoleWindowHelper
    {
        [LibraryImport("Kernel32.dll")]
        private static partial IntPtr GetConsoleWindow();

        [LibraryImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AttachConsole(int dwProcessId);

        [LibraryImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AllocConsole();

        [LibraryImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FreeConsole();

        [LibraryImport("Kernel32.dll")]
        private static partial IntPtr GetStdHandle(int nStdHandle);

        [LibraryImport("Kernel32.dll")]
        private static partial int GetConsoleProcessList(IntPtr lpdwProcessList, int dwProcessCount);

        private static int hasConsole = 0;

        private enum StdHandle
        {
            Stdin = -10,
            Stdout = -11,
            Stderr = -12,
        }


        public static void SetConsoleWindowVisible(bool isShow)
        {
            if (isShow)
            {
                Show();
                Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();
            }
            else
            {
                Hide();
                Log.Instance.RemoveOutput<ConsoleLogOutput>();
            }
        }

        public static void Hide()
        {
            var ori = Interlocked.CompareExchange(ref hasConsole, 0, 1);
            if (ori == 0)
            {
                return;
            }
            FreeConsole();
            Console.SetOut(System.IO.TextWriter.Null);
            Console.SetIn(System.IO.TextReader.Null);
            Console.SetError(System.IO.TextWriter.Null);
        }

        public static void Show()
        {
            var ori = Interlocked.CompareExchange(ref hasConsole, 1, 0);
            if (ori == 1)
            {
                return;
            }
            AllocConsole();
            SafeFileHandle stdoutHandle = new(GetStdHandle((int)StdHandle.Stdout), false);
            SafeFileHandle stdinHandle = new(GetStdHandle((int)StdHandle.Stdin), false);
            SafeFileHandle stderrHandle = new(GetStdHandle((int)StdHandle.Stderr), false);
            var stdoutStream = new System.IO.FileStream(stdoutHandle, System.IO.FileAccess.Write);
            var stdinStream = new System.IO.FileStream(stdinHandle, System.IO.FileAccess.Read);
            var stderrStream = new System.IO.FileStream(stderrHandle, System.IO.FileAccess.Write);
            Console.SetOut(new System.IO.StreamWriter(stdoutStream) { AutoFlush = true });
            Console.SetIn(new System.IO.StreamReader(stdinStream));
            Console.SetError(new System.IO.StreamWriter(stderrStream) { AutoFlush = true });
        }

        public static void AttachConsole()
        {
            AttachConsole(-1);
            SafeFileHandle stdoutHandle = new(GetStdHandle((int)StdHandle.Stdout), false);
            SafeFileHandle stdinHandle = new(GetStdHandle((int)StdHandle.Stdin), false);
            SafeFileHandle stderrHandle = new(GetStdHandle((int)StdHandle.Stderr), false);
            var stdoutStream = new System.IO.FileStream(stdoutHandle, System.IO.FileAccess.Write);
            var stdinStream = new System.IO.FileStream(stdinHandle, System.IO.FileAccess.Read);
            var stderrStream = new System.IO.FileStream(stderrHandle, System.IO.FileAccess.Write);
            Console.SetOut(new System.IO.StreamWriter(stdoutStream) { AutoFlush = true });
            Console.SetIn(new System.IO.StreamReader(stdinStream));
            Console.SetError(new System.IO.StreamWriter(stderrStream) { AutoFlush = true });
        }

    }
}
