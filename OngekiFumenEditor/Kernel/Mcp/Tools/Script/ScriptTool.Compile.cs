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
        [McpServerTool(Name = "script.compile", Title = "Compile Script", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Compile a runtime automation script and run security checks without executing it.")]
        public async Task<ScriptBuildResult> Compile(string scriptText, bool enableSecurityCheck = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "script.compile";
            McpOperationLogHelper.LogRequest(operationName, new
            {
                scriptText,
                enableSecurityCheck,
                requestedBy,
                clientId,
            });

            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync("script.compile", requestedBy, clientId, BuildScriptPreview(scriptText), cancellationToken: cancellationToken);
            if (!authorizationResult.IsAuthorized)
            {
                var deniedResult = new ScriptBuildResult
                {
                    Success = false,
                    SecurityIssues = [authorizationResult.ErrorMessage ?? "Tool authorization was denied."]
                };
                McpOperationLogHelper.LogResult(operationName, deniedResult);
                return deniedResult;
            }

            var result = await scriptHost.BuildAsync(new ScriptBuildRequest
            {
                ScriptText = scriptText,
                EnableSecurityCheck = enableSecurityCheck,
            }, cancellationToken);
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
