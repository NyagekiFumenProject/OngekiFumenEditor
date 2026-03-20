using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IMcpToolAuthorizationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class McpToolAuthorizationService : IMcpToolAuthorizationService
    {
        private const string AnonymousClientIdentityRequiredErrorCode = "MCP_CLIENT_ID_REQUIRED";
        private const string AnonymousClientIdentityRequiredErrorMessage = "Anonymous MCP tool usage is disabled. Provide clientId and/or requestedBy for client identification.";
        private readonly record struct AuthorizationDecision(bool Approved, bool RememberApproval, bool BackupFumenBeforeExecution);

        private readonly IMcpClientAuthorizationManager mcpClientAuthorizationManager;
        private volatile bool backupFumenBeforeScriptExecutionEnabled;

        [ImportingConstructor]
        public McpToolAuthorizationService(IMcpClientAuthorizationManager mcpClientAuthorizationManager)
        {
            this.mcpClientAuthorizationManager = mcpClientAuthorizationManager;
        }

        public async Task<McpToolAuthorizationResult> EnsureAuthorizedAsync(string toolName, string requestedBy, string clientId, string requestPreview, bool allowInteractivePrompt = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            requestedBy = Normalize(requestedBy);
            clientId = Normalize(clientId);
            var registrationInfo = mcpClientAuthorizationManager.RegisterClientUsage(requestedBy, clientId);

            if (!ProgramSetting.Default.AllowAnonymousMcpClientUsage && IsAnonymousClient(requestedBy, clientId))
            {
                McpOperationLogHelper.LogAuthorization(toolName, new
                {
                    requestedBy,
                    clientId,
                    identityKey = registrationInfo?.IdentityKey,
                    approved = false,
                    source = "policy",
                    errorCode = AnonymousClientIdentityRequiredErrorCode,
                    errorMessage = AnonymousClientIdentityRequiredErrorMessage,
                    backupFumenBeforeExecution = backupFumenBeforeScriptExecutionEnabled,
                });
                return McpToolAuthorizationResult.Denied(AnonymousClientIdentityRequiredErrorCode, AnonymousClientIdentityRequiredErrorMessage, backupFumenBeforeScriptExecutionEnabled);
            }

            if (mcpClientAuthorizationManager.IsExecutionApprovalRemembered(requestedBy, clientId))
            {
                McpOperationLogHelper.LogAuthorization(toolName, new
                {
                    requestedBy,
                    clientId,
                    identityKey = registrationInfo?.IdentityKey,
                    approved = true,
                    rememberApproval = true,
                    source = "remembered",
                    backupFumenBeforeExecution = backupFumenBeforeScriptExecutionEnabled,
                });
                return new McpToolAuthorizationResult
                {
                    IsAuthorized = true,
                    BackupFumenBeforeExecution = backupFumenBeforeScriptExecutionEnabled,
                };
            }

            if (!allowInteractivePrompt)
            {
                McpOperationLogHelper.LogAuthorization(toolName, new
                {
                    requestedBy,
                    clientId,
                    identityKey = registrationInfo?.IdentityKey,
                    approved = false,
                    source = "prompt_disabled",
                    errorCode = "USER_CONFIRMATION_REQUIRED",
                    errorMessage = "Tool execution requires user confirmation.",
                    backupFumenBeforeExecution = backupFumenBeforeScriptExecutionEnabled,
                });
                return McpToolAuthorizationResult.Denied("USER_CONFIRMATION_REQUIRED", "Tool execution requires user confirmation.", backupFumenBeforeScriptExecutionEnabled);
            }

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                var decision = ConfirmCore(toolName, requestedBy, clientId, requestPreview);
                LogAuthorizationDecision(toolName, requestedBy, clientId, registrationInfo?.IdentityKey, decision);
                return decision.Approved
                    ? new McpToolAuthorizationResult
                    {
                        IsAuthorized = true,
                        BackupFumenBeforeExecution = decision.BackupFumenBeforeExecution,
                    }
                    : McpToolAuthorizationResult.Denied("USER_CONFIRMATION_REQUIRED", "Tool execution was cancelled by user confirmation.", decision.BackupFumenBeforeExecution);
            }

            var dispatcherDecision = await dispatcher.InvokeAsync(() => ConfirmCore(toolName, requestedBy, clientId, requestPreview)).Task;
            LogAuthorizationDecision(toolName, requestedBy, clientId, registrationInfo?.IdentityKey, dispatcherDecision);
            return dispatcherDecision.Approved
                ? new McpToolAuthorizationResult
                {
                    IsAuthorized = true,
                    BackupFumenBeforeExecution = dispatcherDecision.BackupFumenBeforeExecution,
                }
                : McpToolAuthorizationResult.Denied("USER_CONFIRMATION_REQUIRED", "Tool execution was cancelled by user confirmation.", dispatcherDecision.BackupFumenBeforeExecution);
        }

        private AuthorizationDecision ConfirmCore(string toolName, string requestedBy, string clientId, string requestPreview)
        {
            var isScriptExecutionTool = IsScriptExecutionTool(toolName);
            var dialog = new McpScriptConfirmationDialog(toolName, requestedBy, clientId, requestPreview, isScriptExecutionTool, backupFumenBeforeScriptExecutionEnabled);

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow is not null && mainWindow != dialog)
                dialog.Owner = mainWindow;

            var approved = dialog.ShowDialog() == true;
            var rememberApproval = approved && dialog.RememberApproval;
            if (rememberApproval)
                mcpClientAuthorizationManager.RememberExecutionApproval(requestedBy, clientId);
            if (approved)
                backupFumenBeforeScriptExecutionEnabled = dialog.BackupFumenBeforeExecution;

            return new AuthorizationDecision(approved, rememberApproval, backupFumenBeforeScriptExecutionEnabled);
        }

        private static void LogAuthorizationDecision(string toolName, string requestedBy, string clientId, string identityKey, AuthorizationDecision decision)
        {
            McpOperationLogHelper.LogAuthorization(toolName, new
            {
                requestedBy,
                clientId,
                identityKey,
                approved = decision.Approved,
                rememberApproval = decision.RememberApproval,
                backupFumenBeforeExecution = decision.BackupFumenBeforeExecution,
                source = "dialog",
            });
        }

        private static bool IsScriptExecutionTool(string toolName)
        {
            return string.Equals(toolName, "script.run_current_editor", System.StringComparison.Ordinal) ||
                   string.Equals(toolName, "script.run_editor", System.StringComparison.Ordinal);
        }

        private static bool IsAnonymousClient(string requestedBy, string clientId)
        {
            return string.IsNullOrWhiteSpace(requestedBy) && string.IsNullOrWhiteSpace(clientId);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default : value.Trim();
        }
    }
}
