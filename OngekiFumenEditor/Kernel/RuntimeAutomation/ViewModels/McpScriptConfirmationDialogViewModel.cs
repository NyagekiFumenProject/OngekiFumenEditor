using Caliburn.Micro;
using OngekiFumenEditor.Properties;
using System.Windows;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation.ViewModels
{
    public class McpScriptConfirmationDialogViewModel : Screen
    {
        public string ToolName { get; }
        public string RequestedBy { get; }
        public string ClientId { get; }
        public string RequestPreview { get; }
        public bool IsScriptExecutionTool { get; }

        public string IntroText => string.Format(Resources.McpConfirmToolRequestIntroFormat, ToolName);
        public string MetadataText => BuildMetadataText(ToolName, RequestedBy, ClientId);
        public string RememberApprovalText => HasIdentityInfo(RequestedBy, ClientId) ? Resources.McpRememberApprovalWithIdentity : Resources.McpRememberApprovalAnonymous;
        public Visibility BackupHintVisibility => IsScriptExecutionTool ? Visibility.Collapsed : Visibility.Visible;

        private bool rememberApproval = true;
        public bool RememberApproval
        {
            get => rememberApproval;
            set => Set(ref rememberApproval, value);
        }

        private bool backupFumenBeforeExecution;
        public bool BackupFumenBeforeExecution
        {
            get => backupFumenBeforeExecution;
            set => Set(ref backupFumenBeforeExecution, value);
        }

        public McpScriptConfirmationDialogViewModel(string toolName, string requestedBy, string clientId, string requestPreview, bool isScriptExecutionTool, bool backupFumenBeforeExecution)
        {
            ToolName = toolName ?? string.Empty;
            RequestedBy = requestedBy ?? string.Empty;
            ClientId = clientId ?? string.Empty;
            RequestPreview = requestPreview ?? string.Empty;
            IsScriptExecutionTool = isScriptExecutionTool;
            BackupFumenBeforeExecution = backupFumenBeforeExecution;
        }

        public void OnAllowButtonClicked()
        {
            _ = TryCloseAsync(true);
        }

        public void OnCancelButtonClicked()
        {
            _ = TryCloseAsync(false);
        }

        private static string BuildMetadataText(string toolName, string requestedBy, string clientId)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"{Resources.McpConfirmationToolLabel}: {toolName}"
            };

            if (!HasIdentityInfo(requestedBy, clientId))
                lines.Add($"{Resources.McpConfirmationClientLabel}: {Resources.McpAnonymousClient}");

            if (!string.IsNullOrWhiteSpace(requestedBy))
                lines.Add($"{Resources.McpConfirmationRequestedByLabel}: {requestedBy}");

            if (!string.IsNullOrWhiteSpace(clientId))
                lines.Add($"{Resources.McpConfirmationClientIdLabel}: {clientId}");

            lines.Add(string.Empty);
            lines.Add(Resources.McpConfirmationReviewHint);
            return string.Join(System.Environment.NewLine, lines);
        }

        private static bool HasIdentityInfo(string requestedBy, string clientId)
        {
            return !string.IsNullOrWhiteSpace(requestedBy) || !string.IsNullOrWhiteSpace(clientId);
        }
    }
}
