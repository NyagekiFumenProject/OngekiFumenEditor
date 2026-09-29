using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Script
{
    [Export(typeof(ScriptTool))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed partial class ScriptTool
    {
        private readonly IRuntimeAutomationScriptHost scriptHost;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public ScriptTool(IRuntimeAutomationScriptHost scriptHost, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.scriptHost = scriptHost;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }
    }
}
