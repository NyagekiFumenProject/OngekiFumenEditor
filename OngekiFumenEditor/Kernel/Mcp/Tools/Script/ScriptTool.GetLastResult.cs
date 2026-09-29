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
        [McpServerTool(Name = "script.get_last_result", Title = "Get Last Script Result", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Get the last runtime automation script result returned by the host.")]
        public async Task<ScriptRunResult> GetLastResult(string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "script.get_last_result";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                requestedBy,
                clientId,
            });

            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync("script.get_last_result", requestedBy, clientId, "Read the last runtime automation script result cached by the host.", cancellationToken: cancellationToken);
            if (!authorizationResult.IsAuthorized)
            {
                var deniedResult = new ScriptRunResult
                {
                    Success = false,
                    ErrorCode = authorizationResult.ErrorCode,
                    ErrorMessage = authorizationResult.ErrorMessage,
                };
                McpOperationLogHelper.LogResult(operationName, deniedResult);
                return deniedResult;
            }

            var result = scriptHost.GetLastResult();
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
