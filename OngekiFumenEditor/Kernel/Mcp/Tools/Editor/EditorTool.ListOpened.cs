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
    internal sealed partial class EditorTool
    {
        [McpServerTool(Name = "editor.list_opened", Title = "List Opened Editors", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("List all currently opened editors in the running Ongeki Fumen Editor instance.")]
        public async Task<object> ListOpened(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.list_opened";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                requestedBy,
                clientId,
            });

            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.list_opened", requestedBy, clientId, "List all currently opened editors in the running Ongeki Fumen Editor instance.", cancellationToken: cancellationToken);
            if (!authorizationResult.IsAuthorized)
            {
                var deniedResult = new
                {
                    success = false,
                    errorCode = authorizationResult.ErrorCode,
                    errorMessage = authorizationResult.ErrorMessage,
                };
                McpOperationLogHelper.LogResult(operationName, deniedResult);
                return deniedResult;
            }

            var result = editorDocumentManager.GetEditorSnapshot()
                .Select(EditorContextInfo.From)
                .Where(x => x is not null)
                .ToArray();
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
