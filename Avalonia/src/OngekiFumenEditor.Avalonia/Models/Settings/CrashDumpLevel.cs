namespace OngekiFumenEditor.Avalonia.Models.Settings;

/// <summary>
/// 崩溃转储体积档位，映射到 Windows <c>MiniDumpWriteDump</c> 的标志组合。
/// 仅 Windows 平台生效。
/// </summary>
public enum CrashDumpLevel
{
	/// <summary>小型：仅线程堆栈与模块列表，通常几 MB。</summary>
	Small = 0,

	/// <summary>中型：附加全局数据段、句柄表、已卸载模块、间接引用内存与进程线程数据。</summary>
	Medium = 1,

	/// <summary>完整：包含整个进程内存，通常数百 MB 起。</summary>
	Full = 2,
}
