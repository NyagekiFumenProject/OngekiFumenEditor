using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(EditorTools))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorTools
    {
        private readonly IRuntimeEditorContextProvider editorContextProvider;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public EditorTools(IRuntimeEditorContextProvider editorContextProvider, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.editorContextProvider = editorContextProvider;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }

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

            var result = editorContextProvider.GetCurrentEditor();
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }

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

            var result = editorContextProvider.GetOpenedEditors();
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }

        [McpServerTool(Name = "editor.get_current_summary", Title = "Get Current Editor Summary", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get a lightweight summary of the currently active editor, including file paths and object counts.")]
        public async Task<object> GetCurrentSummary(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.get_current_summary";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                requestedBy,
                clientId,
            });

            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.get_current_summary", requestedBy, clientId, "Read a lightweight summary of the currently active editor, including file paths and object counts.", cancellationToken: cancellationToken);
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

            var current = editorContextProvider.GetCurrentEditor();
            if (current is null)
            {
                var noActiveEditorResult = new
                {
                    success = false,
                    errorCode = "NO_ACTIVE_EDITOR"
                };
                McpOperationLogHelper.LogResult(operationName, noActiveEditorResult);
                return noActiveEditorResult;
            }

            var result = new
            {
                success = true,
                editorId = current.EditorId,
                displayName = current.DisplayName,
                projectPath = current.ProjectPath,
                fumenPath = current.FumenPath,
                isDirty = current.IsDirty,
                isActive = current.IsActive,
                counts = new
                {
                    lanes = current.LaneCount,
                    taps = current.TapCount,
                    holds = current.HoldCount,
                    bells = current.BellCount,
                    bullets = current.BulletCount,
                    bpmChanges = current.BpmChangeCount,
                    soflans = current.SoflanCount,
                }
            };
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
