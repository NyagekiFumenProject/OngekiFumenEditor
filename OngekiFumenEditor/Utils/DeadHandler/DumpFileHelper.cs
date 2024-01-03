using OngekiFumenEditor.Properties;
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

		[DllImport("kernel32.dll", ExactSpelling = true)]
		private static extern UnhandledExceptionFilter SetUnhandledExceptionFilter([MarshalAs(UnmanagedType.FunctionPtr)] UnhandledExceptionFilter lpTopLevelExceptionFilter);

		[DllImport("dbghelp.dll", ExactSpelling = true)]
		private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeHandle hFile, uint DumpType, ref MINIDUMP_EXCEPTION_INFORMATION ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

		[DllImport("kernel32.dll", ExactSpelling = true)]
		private static extern uint GetCurrentThreadId();

		[DllImport("Kernel32.dll")]
		public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);

		public static void Init()
		{
			Directory.CreateDirectory(ProgramSetting.Default.DumpFileDirPath);
			SetUnhandledExceptionFilter(OnWriteMiniDump);
		}

		public static string WriteMiniDump(IntPtr exceptionInfo)
		{
			Directory.CreateDirectory(ProgramSetting.Default.DumpFileDirPath);
			var filePath = Path.GetFullPath(Path.Combine(ProgramSetting.Default.DumpFileDirPath, FileHelper.FilterFileName(DateTime.Now.ToString() + ".dmp")));

			using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

			var currentProcess = Process.GetCurrentProcess();
			var param = new MINIDUMP_EXCEPTION_INFORMATION()
			{
				ThreadId = GetCurrentThreadId(),
				ClientPointers = false,
				ExceptionPointers = exceptionInfo
			};

			// MiniDumpWithFullMemory = 0x00000002
			// MiniDumpNormal = 0x00000000
			var dumpType = ProgramSetting.Default.IsFullDump ? 0x2 : 0x0;

			var isSuccessful = MiniDumpWriteDump(currentProcess.Handle, (uint)currentProcess.Id, fileStream.SafeFileHandle, (uint)dumpType, ref param, IntPtr.Zero, IntPtr.Zero);

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

			Log.LogError($"call MiniDumpWriteDump() exceptionInfo = {exceptionInfo} , dumpType = {dumpType} , isSuccessful = {exceptionInfo} , getLastError = {getErrMsg()} , dumpFilePath = {filePath}");

			//MessageBox.Show(Resources.ProgramThrowAndDump, Resources.ProgramError, MessageBoxButton.OK, MessageBoxImage.Error);

			FileLogOutput.WaitForWriteDone();
			return filePath;
		}

		private static int OnWriteMiniDump(IntPtr exceptionInfo)
		{
			WriteMiniDump(exceptionInfo);
			return 1;
		}
	}
}
