using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal sealed class McpScriptConfirmationDialog : Window
    {
        private readonly CheckBox rememberApprovalCheckBox;
        private readonly CheckBox backupFumenBeforeExecutionCheckBox;

        public McpScriptConfirmationDialog(string toolName, string requestedBy, string clientId, string requestPreview, bool isScriptExecutionTool, bool backupFumenBeforeExecution)
        {
            Title = "Confirm MCP Tool Request";
            Width = 760;
            Height = 560;
            MinWidth = 640;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;

            var root = new Grid
            {
                Margin = new Thickness(16),
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var intro = new TextBlock
            {
                Text = $"About to allow MCP tool call '{toolName}'.",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
            };
            Grid.SetRow(intro, 0);
            root.Children.Add(intro);

            var metadata = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 12),
                Text = BuildMetadataText(toolName, requestedBy, clientId),
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetRow(metadata, 1);
            root.Children.Add(metadata);

            var previewTextBox = new TextBox
            {
                Text = requestPreview ?? string.Empty,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                AcceptsTab = true,
                FontFamily = new FontFamily("Consolas"),
            };
            Grid.SetRow(previewTextBox, 2);
            root.Children.Add(previewTextBox);

            rememberApprovalCheckBox = new CheckBox
            {
                Margin = new Thickness(0, 12, 0, 12),
                Content = BuildRememberApprovalText(requestedBy, clientId),
                IsEnabled = true,
                IsChecked = true,
            };
            Grid.SetRow(rememberApprovalCheckBox, 3);
            root.Children.Add(rememberApprovalCheckBox);

            var backupOptionsPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 12),
            };

            backupFumenBeforeExecutionCheckBox = new CheckBox
            {
                Content = "\u6bcf\u6b21\u4ee3\u7801\u6267\u884c\u524d\u5148\u5907\u4efd\u8c31\u9762\u6587\u4ef6",
                IsChecked = backupFumenBeforeExecution,
            };
            backupOptionsPanel.Children.Add(backupFumenBeforeExecutionCheckBox);

            if (!isScriptExecutionTool)
            {
                backupOptionsPanel.Children.Add(new TextBlock
                {
                    Margin = new Thickness(24, 4, 0, 0),
                    Text = "\u6b64\u9009\u9879\u4f1a\u5e94\u7528\u5230\u540e\u7eed\u7684 script.run_* \u4ee3\u7801\u6267\u884c\u8bf7\u6c42\u3002",
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            Grid.SetRow(backupOptionsPanel, 4);
            root.Children.Add(backupOptionsPanel);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 96,
                MinHeight = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true,
            };
            cancelButton.Click += (_, _) => DialogResult = false;

            var okButton = new Button
            {
                Content = "Allow",
                Width = 96,
                MinHeight = 28,
                IsDefault = true,
            };
            okButton.Click += (_, _) => DialogResult = true;

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            Grid.SetRow(buttons, 5);
            root.Children.Add(buttons);

            Content = root;
        }

        public bool RememberApproval => rememberApprovalCheckBox?.IsChecked == true;
        public bool BackupFumenBeforeExecution => backupFumenBeforeExecutionCheckBox?.IsChecked == true;

        private static string BuildMetadataText(string toolName, string requestedBy, string clientId)
        {
            var anonymousLine = HasIdentityInfo(requestedBy, clientId) ? string.Empty : "\nClient: Anonymous Client";
            var requestedByLine = string.IsNullOrWhiteSpace(requestedBy) ? string.Empty : $"\nRequested by: {requestedBy}";
            var clientIdLine = string.IsNullOrWhiteSpace(clientId) ? string.Empty : $"\nClient ID: {clientId}";
            return $"Tool: {toolName}{anonymousLine}{requestedByLine}{clientIdLine}\n\nReview the request details below before continuing.";
        }

        private static bool HasIdentityInfo(string requestedBy, string clientId)
        {
            return !string.IsNullOrWhiteSpace(requestedBy) || !string.IsNullOrWhiteSpace(clientId);
        }

        private static string BuildRememberApprovalText(string requestedBy, string clientId)
        {
            return HasIdentityInfo(requestedBy, clientId)
                ? "\u9ed8\u8ba4\u5141\u8bb8\u6b64 MCP \u5ba2\u6237\u7aef\u5728\u672c\u6b21\u7a0b\u5e8f\u8fd0\u884c\u671f\u95f4\u7684\u540e\u7eed\u8bf7\u6c42"
                : "\u9ed8\u8ba4\u5141\u8bb8\u672a\u63d0\u4f9b\u5ba2\u6237\u7aef\u6807\u8bc6\u7684 MCP \u8bf7\u6c42\u5728\u672c\u6b21\u7a0b\u5e8f\u8fd0\u884c\u671f\u95f4\u7684\u540e\u7eed\u8bf7\u6c42";
        }
    }
}
