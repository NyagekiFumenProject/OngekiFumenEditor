using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IMcpToolAuthorizationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class McpToolAuthorizationService : IMcpToolAuthorizationService
    {
        private readonly IMcpClientAuthorizationManager mcpClientAuthorizationManager;

        [ImportingConstructor]
        public McpToolAuthorizationService(IMcpClientAuthorizationManager mcpClientAuthorizationManager)
        {
            this.mcpClientAuthorizationManager = mcpClientAuthorizationManager;
        }

        public async Task<bool> EnsureAuthorizedAsync(string toolName, string requestedBy, string clientId, string requestPreview, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            requestedBy = Normalize(requestedBy);
            clientId = Normalize(clientId);
            _ = mcpClientAuthorizationManager.RegisterClientUsage(requestedBy, clientId);

            if (mcpClientAuthorizationManager.IsExecutionApprovalRemembered(requestedBy, clientId))
                return true;

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
                return ConfirmCore(toolName, requestedBy, clientId, requestPreview);

            return await dispatcher.InvokeAsync(() => ConfirmCore(toolName, requestedBy, clientId, requestPreview)).Task;
        }

        private bool ConfirmCore(string toolName, string requestedBy, string clientId, string requestPreview)
        {
            var dialog = new McpScriptConfirmationDialog(toolName, requestedBy, clientId, requestPreview);

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow is not null && mainWindow != dialog)
                dialog.Owner = mainWindow;

            var approved = dialog.ShowDialog() == true;
            if (approved && dialog.RememberApproval)
                mcpClientAuthorizationManager.RememberExecutionApproval(requestedBy, clientId);

            return approved;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? default : value.Trim();
        }
    }
}
