using EditorResources = OngekiFumenEditor.Properties.Resources;
using OngekiFumenEditor.Base.EditorObjects;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Views
{
    public sealed class DeleteSoflanGroupDialog : Window
    {
        public SoflanGroupWrapItem Replacement { get; private set; }
        public bool DeleteReferences { get; private set; }
        public DeleteSoflanGroupDialog(IEnumerable<SoflanGroupWrapItem> groups, int referenceCount)
        {
            Title = EditorResources.SoflanGroupDeleteReferenced; Width = 440; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = string.Format(EditorResources.SoflanGroupReferenceCount, referenceCount), TextWrapping = TextWrapping.Wrap });
            var selector = new ComboBox { ItemsSource = groups.ToArray(), DisplayMemberPath = "SelectionDisplayName", Margin = new Thickness(0, 12, 0, 12), SelectedIndex = 0 };
            panel.Children.Add(selector);
            void Add(string text, System.Action action) { var button = new Button { Content = text, Margin = new Thickness(0, 4, 0, 4) }; button.Click += (_, _) => action(); panel.Children.Add(button); }
            Add(EditorResources.SoflanGroupReplaceAndDelete, () => { if (selector.SelectedItem is not SoflanGroupWrapItem item) return; Replacement = item; DialogResult = true; });
            Add(EditorResources.SoflanGroupDeleteAllReferences, () => { DeleteReferences = true; DialogResult = true; });
            Add(EditorResources.Cancel, () => DialogResult = false);
            Content = panel;
        }
    }
}
