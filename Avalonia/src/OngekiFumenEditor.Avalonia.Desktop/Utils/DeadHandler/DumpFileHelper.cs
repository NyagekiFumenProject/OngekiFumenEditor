using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils.DeadHandler
{
	public static class DumpFileHelper
	{
		// dbghelp.dll MINIDUMP_TYPE flags (winnt/dbghelp.h)
		private const uint MiniDumpNormal = 0x0000_0000;
		private const uint MiniDumpWithDataSegs = 0x0000_0001;
		private const uint MiniDumpWithFullMemory = 0x0000_0002;
		private const uint MiniDumpWithHandleData = 0x0000_0004;
		private const uint MiniDumpWithUnloadedModules = 0x0000_0020;
		private const uint MiniDumpWithIndirectlyReferencedMemory = 0x0000_0040;
		private const uint MiniDumpWithProcessThreadData = 0x0000_0100;
		private const uint MiniDumpWithFullMemoryInfoData = 0x0000_0800;
		private const uint MiniDumpWithThreadInfo = 0x0000_1000;

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
		private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeHandle hFile, uint DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

		[DllImport("kernel32.dll", ExactSpelling = true)]
		private static extern uint GetCurrentThreadId();

		[DllImport("Kernel32.dll")]
		public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);

		private static int filterInstalled;

		/// <summary>
		///     注册 Win32 顶层未处理异常过滤器：托管管道覆盖不到的原生崩溃
		///     （P/Invoke 访问冲突、C++ 异常等）也会写出 minidump 后终止进程。
		/// </summary>
		/// <remarks>必须在 DI 容器构建完成后调用（内部要读取 ProgramSetting）。</remarks>
		public static void Init()
		{
			if (Interlocked.Exchange(ref filterInstalled, 1) != 0)
				return;

			SetUnhandledExceptionFilter(OnWriteMiniDump);
		}

		private static string EnsureDumpDirectory()
		{
			var dirPath = ProgramSetting.Default.DumpFileDirPath;
			Directory.CreateDirectory(dirPath);
			return dirPath;
		}

		/// <summary>按用户选择的 <see cref="CrashDumpLevel"/> 解析 MINIDUMP_TYPE 标志组合。</summary>
		private static uint GetDumpFlags() => ProgramSetting.Default.DumpLevel switch
		{
			CrashDumpLevel.Medium => MiniDumpNormal
				| MiniDumpWithDataSegs
				| MiniDumpWithHandleData
				| MiniDumpWithUnloadedModules
				| MiniDumpWithIndirectlyReferencedMemory
				| MiniDumpWithProcessThreadData,
			CrashDumpLevel.Full => MiniDumpWithFullMemory
				| MiniDumpWithHandleData
				| MiniDumpWithUnloadedModules
				| MiniDumpWithProcessThreadData
				| MiniDumpWithFullMemoryInfoData
				| MiniDumpWithThreadInfo,
			_ => MiniDumpNormal,
		};

		/// <summary>
		///     写出 minidump 并返回文件路径。<paramref name="exceptionInfo"/> 为
		///     <see cref="IntPtr.Zero"/>（纯托管异常没有原生 EXCEPTION_POINTERS）时，
		///     写不带异常上下文的转储：仍包含全线程栈，可事后配合 SOS 分析。
		/// </summary>
		public static string WriteMiniDump(IntPtr exceptionInfo)
		{
			var filePath = Path.GetFullPath(Path.Combine(EnsureDumpDirectory(), FileHelper.FilterFileName(DateTime.Now.ToString() + ".dmp")));

			using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

			var currentProcess = Process.GetCurrentProcess();
			var dumpType = GetDumpFlags();

			var exceptionParam = IntPtr.Zero;
			if (exceptionInfo != IntPtr.Zero)
			{
				exceptionParam = Marshal.AllocHGlobal(Marshal.SizeOf<MINIDUMP_EXCEPTION_INFORMATION>());
				Marshal.StructureToPtr(
					new MINIDUMP_EXCEPTION_INFORMATION()
					{
						ThreadId = GetCurrentThreadId(),
						ClientPointers = false,
						ExceptionPointers = exceptionInfo
					},
					exceptionParam,
					fDeleteOld: false);
			}

			try
			{
				var isSuccessful = MiniDumpWriteDump(currentProcess.Handle, (uint)currentProcess.Id, fileStream.SafeFileHandle, dumpType, exceptionParam, IntPtr.Zero, IntPtr.Zero);

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

				Log.LogError($"call MiniDumpWriteDump() exceptionInfo = {exceptionInfo} , dumpType = {dumpType} , isSuccessful = {isSuccessful} , getLastError = {getErrMsg()} , dumpFilePath = {filePath}");
			}
			finally
			{
				if (exceptionParam != IntPtr.Zero)
					Marshal.FreeHGlobal(exceptionParam);
			}

			DesktopFileLogOutput.WaitForWriteDone();
			return filePath;
		}

		private static int OnWriteMiniDump(IntPtr exceptionInfo)
		{
			WriteMiniDump(exceptionInfo);
			// EXCEPTION_EXECUTE_HANDLER：写完 dump 即终止进程。
			return 1;
		}
	}
}
