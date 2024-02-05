using Caliburn.Micro;
using OngekiFumenEditor.Utils.Logs;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils
{
    [Export(typeof(Log))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class Log
    {
        private List<ILogOutput> outputs = new List<ILogOutput>();
        private IEnumerable<ILogOutput> LogOutputs => outputs;

        private StringBuilder sb = new StringBuilder(2048);

        private static Log cacheInstance;
        public static Log Instance => cacheInstance ?? (cacheInstance = IoC.Get<Log>());

        [ImportingConstructor]
        public Log([ImportMany] IEnumerable<ILogOutput> outputs)
        {
            this.outputs.AddRange(outputs);
        }

        public void RemoveOutputIfNotExist<T>() where T : ILogOutput
        {
            outputs.RemoveAll(x => x is T);
        }

        public void AddOutputIfNotExist<T>() where T : ILogOutput, new()
        {
            if (outputs.OfType<T>().Any())
                return;
            outputs.Add(new T());
        }

        internal void Output(Severity severity, string message)
        {
            foreach (var output in LogOutputs)
                output.WriteLog(severity, message);
        }

        private string BuildLogMessage(string message, Severity severity, bool new_line, bool time, string prefix)
        {
            lock (sb)
            {
                sb.Clear();

                sb.AppendFormat("[{0} {1}:{2}]", time ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : string.Empty, severity.ToString().ToUpper(), Thread.CurrentThread.ManagedThreadId);

                if (!string.IsNullOrWhiteSpace(prefix))
                    sb.AppendFormat("{0}", prefix);

                sb.AppendFormat(":{0}", message);

                if (new_line)
                    sb.AppendLine();

                return sb.ToString();
            }
        }

        public static void LogDebug(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var severity = Severity.Debug;
            var msg = instance.BuildLogMessage(message, severity, newLine, time, prefix);
            instance.Output(severity, msg);
        }

        public static void LogInfo(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var severity = Severity.Info;
            var msg = instance.BuildLogMessage(message, severity, newLine, time, prefix);
            instance.Output(severity, msg);
        }

        public static void LogWarn(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var severity = Severity.Warn;
            var msg = instance.BuildLogMessage(message, severity, newLine, time, prefix);
            instance.Output(severity, msg);
        }

        public static void LogError(string message, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var severity = Severity.Error;
            var msg = instance.BuildLogMessage(message, severity, newLine, time, prefix);
            instance.Output(severity, msg);
        }

        public static void LogError(string message, Exception e, bool newLine = true, bool time = true, [CallerMemberName] string prefix = "<Unknown>")
        {
            var instance = Instance;
            var severity = Severity.Error;
            var msg = instance.BuildLogMessage($"{message}\nContains exception:{e.Message}\n{e.StackTrace}", severity, newLine, time, prefix);
            instance.Output(severity, msg);
        }
    }
}
