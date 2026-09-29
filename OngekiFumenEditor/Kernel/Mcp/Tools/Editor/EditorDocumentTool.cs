using Caliburn.Micro;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    [Export(typeof(EditorDocumentTool))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed partial class EditorDocumentTool
    {
        private static readonly string[] FastOpenExtensions = { ".ogkr", ".nyageki" };

        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public EditorDocumentTool(IEditorDocumentManager editorDocumentManager, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }
    }
}
