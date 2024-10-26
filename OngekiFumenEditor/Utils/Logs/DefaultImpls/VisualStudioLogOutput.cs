using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
#if DEBUG
    [Export(typeof(ILogOutput))]
    internal class VisualStudioLogOutput : ILogOutput
    {
        internal VisualStudioLogOutput()
        {
        }

        public void WriteLog(Severity severity, string content)
        {
            Debug.Write(content);
        }
    }
#endif
}
