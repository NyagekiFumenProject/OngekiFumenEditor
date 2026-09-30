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
    internal sealed partial class EditorDocumentTool
    {
        [McpServerTool(Name = "editor.open_proj", Title = "Open Project", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Open a .nyagekiProj project file. The editor tab is created immediately while its chart and audio keep loading in the background (the editor shows a loading dialog); poll editor.get_current_summary until counts are populated before reading chart content.")]
        public async Task<object> OpenProject(string projectPath, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.open_proj";
            McpOperationLogHelper.LogRequest(operationName, new { projectPath, requireConfirmation, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Open project '{projectPath}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return Failure(operationName, "FILE_NOT_FOUND", $"Project file not found: {projectPath}");

            if (!string.Equals(Path.GetExtension(projectPath), FumenVisualEditorProvider.FILE_EXTENSION_NAME, StringComparison.OrdinalIgnoreCase))
                return Failure(operationName, "UNSUPPORTED_FILE_TYPE", $"editor.open_proj expects a {FumenVisualEditorProvider.FILE_EXTENSION_NAME} file; use editor.open_fast for .ogkr/.nyageki charts.");

            bool opened;
            try
            {
                opened = await RuntimeUiDispatcher.RunAsync(async () => await DocumentOpenHelper.TryOpenAsDocument(projectPath), cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "OPEN_FAILED", ex.Message);
            }

            return ReportOpen(operationName, opened, default, default, projectPath);
        }
    }
}
