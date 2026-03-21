using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Res = OngekiFumenEditor.Properties.Resources;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal sealed class McpScriptConfirmationDialog : Window
    {
        private readonly CheckBox rememberApprovalCheckBox;
        private readonly CheckBox backupFumenBeforeExecutionCheckBox;

        public McpScriptConfirmationDialog(string toolName, string requestedBy, string clientId, string requestPreview, bool isScriptExecutionTool, bool backupFumenBeforeExecution)
        {
            Title = Res.McpConfirmToolRequestTitle;
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
                Text = string.Format(Res.McpConfirmToolRequestIntroFormat, toolName),
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
                Content = Res.McpBackupFumenBeforeExecution,
                IsChecked = backupFumenBeforeExecution,
            };
            backupOptionsPanel.Children.Add(backupFumenBeforeExecutionCheckBox);

            if (!isScriptExecutionTool)
            {
                backupOptionsPanel.Children.Add(new TextBlock
                {
                    Margin = new Thickness(24, 4, 0, 0),
                    Text = Res.McpBackupFumenHintForNonScriptTool,
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
                Content = Res.Cancel,
                Width = 96,
                MinHeight = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true,
            };
            cancelButton.Click += (_, _) => DialogResult = false;

            var okButton = new Button
            {
                Content = Res.McpAllow,
                Width = 96,
                MinHeight = 28,
                IsDefault = true,
                Margin = new Thickness(0,0,10,0)
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
            var lines = new System.Collections.Generic.List<string>
            {
                $"{Res.McpConfirmationToolLabel}: {toolName}"
            };

            if (!HasIdentityInfo(requestedBy, clientId))
                lines.Add($"{Res.McpConfirmationClientLabel}: {Res.McpAnonymousClient}");

            if (!string.IsNullOrWhiteSpace(requestedBy))
                lines.Add($"{Res.McpConfirmationRequestedByLabel}: {requestedBy}");

            if (!string.IsNullOrWhiteSpace(clientId))
                lines.Add($"{Res.McpConfirmationClientIdLabel}: {clientId}");

            lines.Add(string.Empty);
            lines.Add(Res.McpConfirmationReviewHint);
            return string.Join(System.Environment.NewLine, lines);
        }

        private static bool HasIdentityInfo(string requestedBy, string clientId)
        {
            return !string.IsNullOrWhiteSpace(requestedBy) || !string.IsNullOrWhiteSpace(clientId);
        }

        private static string BuildRememberApprovalText(string requestedBy, string clientId)
        {
            return HasIdentityInfo(requestedBy, clientId)
                ? Res.McpRememberApprovalWithIdentity
                : Res.McpRememberApprovalAnonymous;
        }
    }
}
