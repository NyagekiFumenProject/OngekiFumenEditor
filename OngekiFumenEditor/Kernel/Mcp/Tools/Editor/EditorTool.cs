using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    [Export(typeof(EditorTool))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed partial class EditorTool
    {
        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public EditorTool(IEditorDocumentManager editorDocumentManager, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }
    }
}
