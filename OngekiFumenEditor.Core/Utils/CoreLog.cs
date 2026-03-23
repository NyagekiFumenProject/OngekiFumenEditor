using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils
{
    public enum CoreLogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
    }

    public interface ICoreLogTarget
    {
        void Write(CoreLogLevel level, string message, Exception exception, string memberName, string filePath, int lineNumber);
    }

    public static class CoreLog
    {
        private sealed class DebugCoreLogTarget : ICoreLogTarget
        {
            public void Write(CoreLogLevel level, string message, Exception exception, string memberName, string filePath, int lineNumber)
            {
                var output = $"[{level}] {message}";
                if (exception is not null)
                    output += $"{Environment.NewLine}{exception}";
                Debug.WriteLine(output);
            }
        }

        private static readonly ICoreLogTarget defaultTarget = new DebugCoreLogTarget();
        private static Func<ICoreLogTarget> resolver = () => defaultTarget;

        public static void SetResolver(Func<ICoreLogTarget> targetResolver)
        {
            resolver = targetResolver ?? (() => defaultTarget);
        }

        public static void ResetResolver()
        {
            resolver = () => defaultTarget;
        }

        public static void LogDebug(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Write(CoreLogLevel.Debug, message, null, memberName, filePath, lineNumber);
        }

        public static void LogInfo(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Write(CoreLogLevel.Info, message, null, memberName, filePath, lineNumber);
        }

        public static void LogWarn(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Write(CoreLogLevel.Warn, message, null, memberName, filePath, lineNumber);
        }

        public static void LogError(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Write(CoreLogLevel.Error, message, null, memberName, filePath, lineNumber);
        }

        public static void LogError(string message, Exception exception,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Write(CoreLogLevel.Error, message, exception, memberName, filePath, lineNumber);
        }

        private static void Write(CoreLogLevel level, string message, Exception exception, string memberName, string filePath, int lineNumber)
        {
            ResolveTarget().Write(level, message, exception, memberName, filePath, lineNumber);
        }

        private static ICoreLogTarget ResolveTarget()
        {
            try
            {
                return resolver?.Invoke() ?? defaultTarget;
            }
            catch
            {
                return defaultTarget;
            }
        }
    }
}

