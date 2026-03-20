using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(ScriptTools))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ScriptTools
    {
        private readonly IRuntimeAutomationScriptHost scriptHost;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public ScriptTools(IRuntimeAutomationScriptHost scriptHost, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.scriptHost = scriptHost;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }

        [McpServerTool(Name = "script.compile", Title = "Compile Script", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Compile a runtime automation script and run security checks without executing it.")]
        public async Task<ScriptBuildResult> Compile(string scriptText, bool enableSecurityCheck = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            if (!await mcpToolAuthorizationService.EnsureAuthorizedAsync("script.compile", requestedBy, clientId, BuildScriptPreview(scriptText), cancellationToken))
            {
                return new ScriptBuildResult
                {
                    Success = false,
                    SecurityIssues = ["Tool authorization was denied by user confirmation."]
                };
            }

            return await scriptHost.BuildAsync(new ScriptBuildRequest
            {
                ScriptText = scriptText,
                EnableSecurityCheck = enableSecurityCheck,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.run_current_editor", Title = "Run Script On Current Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against the currently active editor.")]
        public Task<ScriptRunResult> RunCurrentEditor(string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            return scriptHost.RunOnCurrentEditorAsync(new ScriptRunRequest
            {
                ScriptText = scriptText,
                ExpectedEditorId = expectedEditorId,
                RequireConfirmation = requireConfirmation,
                WrapUndoTransaction = wrapUndoTransaction,
                TransactionName = transactionName,
                RequestedBy = requestedBy,
                ClientId = clientId,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.run_editor", Title = "Run Script On Specified Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against a specific opened editor.")]
        public Task<ScriptRunResult> RunEditor(string editorId, string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            return scriptHost.RunOnEditorAsync(editorId, new ScriptRunRequest
            {
                ScriptText = scriptText,
                ExpectedEditorId = expectedEditorId,
                RequireConfirmation = requireConfirmation,
                WrapUndoTransaction = wrapUndoTransaction,
                TransactionName = transactionName,
                RequestedBy = requestedBy,
                ClientId = clientId,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.get_last_result", Title = "Get Last Script Result", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get the last runtime automation script result returned by the host.")]
        public async Task<ScriptRunResult> GetLastResult(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            if (!await mcpToolAuthorizationService.EnsureAuthorizedAsync("script.get_last_result", requestedBy, clientId, "Read the last runtime automation script result cached by the host.", cancellationToken))
            {
                return new ScriptRunResult
                {
                    Success = false,
                    ErrorCode = "USER_CONFIRMATION_REQUIRED",
                    ErrorMessage = "Tool execution was cancelled by user confirmation.",
                };
            }

            return scriptHost.GetLastResult();
        }

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
