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
        public async Task<EditorContextInfo> GetCurrent(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            if (!await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.get_current", requestedBy, clientId, "Read the currently active editor in the running Ongeki Fumen Editor instance.", cancellationToken))
                return default;

            return editorContextProvider.GetCurrentEditor();
        }

        [McpServerTool(Name = "editor.list_opened", Title = "List Opened Editors", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("List all currently opened editors in the running Ongeki Fumen Editor instance.")]
        public async Task<IReadOnlyList<EditorContextInfo>> ListOpened(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            if (!await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.list_opened", requestedBy, clientId, "List all currently opened editors in the running Ongeki Fumen Editor instance.", cancellationToken))
                return [];

            return editorContextProvider.GetOpenedEditors();
        }

        [McpServerTool(Name = "editor.get_current_summary", Title = "Get Current Editor Summary", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get a lightweight summary of the currently active editor, including file paths and object counts.")]
        public async Task<object> GetCurrentSummary(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            if (!await mcpToolAuthorizationService.EnsureAuthorizedAsync("editor.get_current_summary", requestedBy, clientId, "Read a lightweight summary of the currently active editor, including file paths and object counts.", cancellationToken))
                return new
                {
                    success = false,
                    errorCode = "USER_CONFIRMATION_REQUIRED"
                };

            var current = editorContextProvider.GetCurrentEditor();
            if (current is null)
            {
                return new
                {
                    success = false,
                    errorCode = "NO_ACTIVE_EDITOR"
                };
            }

            return new
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
        }
    }
}
