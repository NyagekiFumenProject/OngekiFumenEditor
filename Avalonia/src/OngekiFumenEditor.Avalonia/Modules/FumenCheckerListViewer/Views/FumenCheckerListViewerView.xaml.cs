using Avalonia.Controls;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Views;

public partial class FumenCheckerListViewerView : ViewBase
{
    public FumenCheckerListViewerView()
    {
        InitializeComponent();
    }

    private void OnCheckResultGridLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FumenCheckerListViewerViewModel viewModel)
            viewModel.RefreshFilter();
    }
}
