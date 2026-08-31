using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.Modules.Settings.ViewModels;
using Gekimini.Avalonia.Modules.Settings.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class SettingsViewTests
{
    [AvaloniaFact]
    public void SettingsView_ExpandsNavigationAndSelectsFirstLeafEditor()
    {
        var application = Assert.IsType<TestApplication>(Application.Current);
        var editors = application.ServiceProvider
            .GetServices<ISettingsEditor>()
            .ToArray();
        var viewModel = new SettingsViewModel(editors);
        var view = new SettingsView { DataContext = viewModel };
        var window = new Window
        {
            Width = 800,
            Height = 450,
            Content = view
        };

        try
        {
            window.Show();
            window.UpdateLayout();

            var tree = Assert.IsType<TreeView>(view.FindControl<TreeView>("treeView"));
            var treeItems = tree.GetVisualDescendants().OfType<TreeViewItem>().ToArray();

            Assert.NotEmpty(treeItems);
            Assert.All(treeItems, item => Assert.True(item.IsExpanded));
            Assert.Same(viewModel.SelectedPage, tree.SelectedItem);

            var selectedItems = treeItems.Where(item => item.IsSelected).ToArray();
            Assert.Single(selectedItems);
            Assert.Same(viewModel.SelectedPage, selectedItems[0].DataContext);
            Assert.Same(viewModel.Pages[0].Children[0], viewModel.SelectedPage);

            var selectedEditor = Assert.Single(viewModel.SelectedPage.Editors);
            Assert.Contains(
                view.GetVisualDescendants().OfType<Control>(),
                control => ReferenceEquals(control.DataContext, selectedEditor));
        }
        finally
        {
            window.Close();
        }
    }
}
