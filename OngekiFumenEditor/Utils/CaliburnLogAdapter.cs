#if DEBUG
using System;
using System.Diagnostics;
using Caliburn.Micro;

namespace OngekiFumenEditor.Utils;

/// <summary>
/// 将 Caliburn.Micro 内部日志转发到项目日志管道。仅在 Debug 构建启用，
/// 消息保留来源类型名，调用者标签固定为 <c>CMLog</c>；转发失败时退回 Debug 输出，
/// 不影响 Caliburn 自身的控制流。
/// </summary>
internal sealed class CaliburnLogAdapter : ILog
{
    private const string LogPrefix = "CMLog";

    private readonly string displayTypeName;

    public CaliburnLogAdapter(Type type)
    {
        displayTypeName = (type?.FullName ?? string.Empty).TrimStart("Caliburn.Micro.");
    }

    public void Info(string format, params object[] args)
        => Forward(CoreLogLevel.Debug, Format(format, args), null); //redirect INFO to DEBUG

    public void Warn(string format, params object[] args)
        => Forward(CoreLogLevel.Warn, Format(format, args), null);

    public void Error(Exception exception)
        => Forward(CoreLogLevel.Error, exception?.Message ?? "Unknown Caliburn.Micro error", exception);

    private void Forward(CoreLogLevel level, string message, Exception exception)
    {
        var actualMessage = $"{displayTypeName}: {message}";
        try
        {
            switch (level)
            {
                case CoreLogLevel.Info:
                    Log.LogInfo(actualMessage, prefix: LogPrefix);
                    break;
                case CoreLogLevel.Warn:
                    Log.LogWarn(actualMessage, prefix: LogPrefix);
                    break;
                case CoreLogLevel.Error when exception is not null:
                    Log.LogError(actualMessage, exception, prefix: LogPrefix);
                    break;
                case CoreLogLevel.Error:
                    Log.LogError(actualMessage, prefix: LogPrefix);
                    break;
            }
        }
        catch (Exception logError)
        {
            // 日志初始化或 IoC 尚未就绪时不能让 Caliburn 的控制流失败。
            Debug.WriteLine($"{actualMessage} (Caliburn log forwarding failed: {logError.Message})");
        }
    }

    private static string Format(string format, object[] args)
    {
        if (string.IsNullOrEmpty(format))
            return string.Empty;
        if (args is null || args.Length == 0)
            return format;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format + " " + string.Join(", ", args);
        }
    }
}
#endif
