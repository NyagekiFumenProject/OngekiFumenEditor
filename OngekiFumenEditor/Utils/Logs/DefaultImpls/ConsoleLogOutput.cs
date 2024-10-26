using Gemini.Modules.Output;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    [Export(typeof(ILogOutput))]
    internal class ConsoleLogOutput : ILogOutput
    {
        public void WriteLog(Severity severity, string content)
        {
            var backup = Console.ForegroundColor;
            Console.ForegroundColor = severity switch
            {
                Severity.Debug => ConsoleColor.Gray,
                Severity.Info => ConsoleColor.Green,
                Severity.Warn => ConsoleColor.Yellow,
                Severity.Error => ConsoleColor.Red,
                _ => ConsoleColor.Cyan,
            };
            Console.Write(content);
            Console.ForegroundColor = backup;
        }
    }
}
