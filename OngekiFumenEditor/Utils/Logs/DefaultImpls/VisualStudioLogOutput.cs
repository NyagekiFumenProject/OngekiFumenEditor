using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    [Export(typeof(ILogOutput))]
    internal class VisualStudioLogOutput : ILogOutput
    {
        public void WriteLog(Severity severity , string content)
        {
#if DEBUG
            Debug.Write(content);
#endif
        }
    }
}
