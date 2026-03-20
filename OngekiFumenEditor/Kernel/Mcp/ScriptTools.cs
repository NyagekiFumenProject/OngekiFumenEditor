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

        [ImportingConstructor]
        public ScriptTools(IRuntimeAutomationScriptHost scriptHost)
        {
            this.scriptHost = scriptHost;
        }

        [McpServerTool(Name = "script.compile", Title = "Compile Script", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Compile a runtime automation script and run security checks without executing it.")]
        public Task<ScriptBuildResult> Compile(string scriptText, bool enableSecurityCheck = true, CancellationToken cancellationToken = default)
        {
            return scriptHost.BuildAsync(new ScriptBuildRequest
            {
                ScriptText = scriptText,
                EnableSecurityCheck = enableSecurityCheck,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.run_current_editor", Title = "Run Script On Current Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against the currently active editor.")]
        public Task<ScriptRunResult> RunCurrentEditor(string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, CancellationToken cancellationToken = default)
        {
            return scriptHost.RunOnCurrentEditorAsync(new ScriptRunRequest
            {
                ScriptText = scriptText,
                ExpectedEditorId = expectedEditorId,
                RequireConfirmation = requireConfirmation,
                WrapUndoTransaction = wrapUndoTransaction,
                TransactionName = transactionName,
                RequestedBy = requestedBy,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.run_editor", Title = "Run Script On Specified Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against a specific opened editor.")]
        public Task<ScriptRunResult> RunEditor(string editorId, string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, CancellationToken cancellationToken = default)
        {
            return scriptHost.RunOnEditorAsync(editorId, new ScriptRunRequest
            {
                ScriptText = scriptText,
                ExpectedEditorId = expectedEditorId,
                RequireConfirmation = requireConfirmation,
                WrapUndoTransaction = wrapUndoTransaction,
                TransactionName = transactionName,
                RequestedBy = requestedBy,
            }, cancellationToken);
        }

        [McpServerTool(Name = "script.get_last_result", Title = "Get Last Script Result", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get the last runtime automation script result returned by the host.")]
        public ScriptRunResult GetLastResult()
        {
            return scriptHost.GetLastResult();
        }
    }
}
