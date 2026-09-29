using OngekiFumenEditor.Kernel.RuntimeAutomation;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Script
{
    internal sealed partial class ScriptTool
    {
        [McpServerTool(Name = "script.run_current_editor", Title = "Run Script On Current Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against the currently active editor.")]
        public async Task<ScriptRunResult> RunCurrentEditor(string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "script.run_current_editor";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                scriptText,
                expectedEditorId,
                requireConfirmation,
                wrapUndoTransaction,
                transactionName,
                requestedBy,
                clientId,
            });

            var result = await scriptHost.RunOnCurrentEditorAsync(new ScriptRunRequest
            {
                ScriptText = scriptText,
                ExpectedEditorId = expectedEditorId,
                RequireConfirmation = requireConfirmation,
                WrapUndoTransaction = wrapUndoTransaction,
                TransactionName = transactionName,
                RequestedBy = requestedBy,
                ClientId = clientId,
            }, cancellationToken);
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
