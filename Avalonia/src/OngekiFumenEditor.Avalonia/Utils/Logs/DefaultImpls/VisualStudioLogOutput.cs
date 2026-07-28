using Injectio.Attributes;
using System.Diagnostics;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

#if DEBUG
[RegisterSingleton<ILogOutput>]
internal class VisualStudioLogOutput : ILogOutput
{
    public void WriteLog(Severity severity, string content)
    {
        Debug.Write(content);
    }
}
#endif


