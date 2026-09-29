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
        [McpServerTool(Name = "script.run_editor", Title = "Run Script On Specified Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Execute a runtime automation script against a specific opened editor.")]
        public async Task<ScriptRunResult> RunEditor(string editorId, string scriptText, string expectedEditorId = default, bool requireConfirmation = true, bool wrapUndoTransaction = true, string transactionName = default, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "script.run_editor";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                editorId,
                scriptText,
                expectedEditorId,
                requireConfirmation,
                wrapUndoTransaction,
                transactionName,
                requestedBy,
                clientId,
            });

            var result = await scriptHost.RunOnEditorAsync(editorId, new ScriptRunRequest
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
