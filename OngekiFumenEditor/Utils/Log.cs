using Caliburn.Micro;
using Gemini.Modules.Output;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public interface ILogOutput
    {
        public void WriteLog(string content);
    }

    [Export(typeof(ILogOutput))]
    public class GeminiLogOutput : ILogOutput
    {
        [Import(typeof(IOutput))]
        public IOutput Output { get; private set; }

        public void WriteLog(string content)
        {
            Output?.Append(content);
        }
    }

    [Export(typeof(Log))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class Log
    {
        [ImportMany]
        IEnumerable<ILogOutput> LogOutputs { get; set; }

        private StringBuilder sb = new StringBuilder(2048);

        private static Log cacheInstance;
        public static Log Instance => cacheInstance ?? (cacheInstance = IoC.Get<Log>());

        internal void Output(string message)
        {
            foreach (var output in LogOutputs)
            {
                output.WriteLog(message);
            }
        }

        private string BuildLogMessage(string message, string type, bool new_line, bool time, string prefix)
        {
            lock (sb)
            {
                sb.Clear();

                sb.AppendFormat("[{0} {1}:{2}]", time ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : string.Empty, type, Thread.CurrentThread.ManagedThreadId);

                if (!string.IsNullOrWhiteSpace(prefix))
                    sb.AppendFormat("{0}", prefix);

                sb.AppendFormat(":{0}", message);

                if (new_line)
                    sb.AppendLine();

                return sb.ToString();
            }
        }

        public static void LogInfo(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var msg = instance.BuildLogMessage(message, "INFO", newLine, time, prefix);
            instance.Output(msg);
        }

        public static void LogWarn(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var msg = instance.BuildLogMessage(message, "WARN", newLine, time, prefix);
            instance.Output(msg);
        }

        public static void LogError(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var msg = instance.BuildLogMessage(message, "ERROR", newLine, time, prefix);
            instance.Output(msg);
        }
    }
}
