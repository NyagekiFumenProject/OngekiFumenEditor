using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal sealed class McpScriptConfirmationDialog : Window
    {
        private readonly CheckBox rememberApprovalCheckBox;

        public McpScriptConfirmationDialog(string toolName, string requestedBy, string clientId, string requestPreview)
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
                Content = "Always allow requests from this MCP client for the rest of this app session",
                IsEnabled = HasIdentityInfo(requestedBy, clientId),
                Visibility = HasIdentityInfo(requestedBy, clientId) ? Visibility.Visible : Visibility.Collapsed,
            };
            Grid.SetRow(rememberApprovalCheckBox, 3);
            root.Children.Add(rememberApprovalCheckBox);

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

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(okButton);

            Grid.SetRow(buttons, 4);
            root.Children.Add(buttons);

            Content = root;
        }

        public bool RememberApproval => rememberApprovalCheckBox?.IsChecked == true;

        private static string BuildMetadataText(string toolName, string requestedBy, string clientId)
        {
            var requestedByLine = string.IsNullOrWhiteSpace(requestedBy) ? string.Empty : $"\nRequested by: {requestedBy}";
            var clientIdLine = string.IsNullOrWhiteSpace(clientId) ? string.Empty : $"\nClient ID: {clientId}";
            return $"Tool: {toolName}{requestedByLine}{clientIdLine}\n\nReview the request details below before continuing.";
        }

        private static bool HasIdentityInfo(string requestedBy, string clientId)
        {
            return !string.IsNullOrWhiteSpace(requestedBy) || !string.IsNullOrWhiteSpace(clientId);
        }
    }
}
