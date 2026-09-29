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
        [McpServerTool(Name = "editor.get_current", Title = "Get Current Editor", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get the currently active editor in the running Ongeki Fumen Editor instance.")]
        public async Task<object> GetCurrent(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.get_current";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                requestedBy,
                clientId,
            });

            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.get_current", requestedBy, clientId, "Read the currently active editor in the running Ongeki Fumen Editor instance.", cancellationToken: cancellationToken);
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

            var result = EditorContextInfo.From(editorDocumentManager.CurrentActivatedEditor);
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
