using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Script
{
    internal sealed partial class ScriptTool
    {
        private static string BuildScriptPreview(string scriptText)
        {
            scriptText ??= string.Empty;
            scriptText = scriptText.Replace("\r\n", "\n");
            if (scriptText.Length > 400)
                scriptText = scriptText[..400] + "\n...";

            return scriptText;
        }
    }
}
