using Injectio.Attributes;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

[RegisterSingleton<ILogOutput>]
public class GeminiLogOutput : ILogOutput
{
    public void WriteLog(Severity severity, string content)
    {
        Console.Write(content);
    }
}
