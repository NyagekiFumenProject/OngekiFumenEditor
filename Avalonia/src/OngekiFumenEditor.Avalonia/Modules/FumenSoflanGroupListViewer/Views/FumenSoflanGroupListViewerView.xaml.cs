using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Views;

public partial class FumenSoflanGroupListViewerView : UserControl
{
    public FumenSoflanGroupListViewerView()
    {
        InitializeComponent();
    }

    private void OnItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SoflanGroupWrapItem item } &&
            DataContext is FumenSoflanGroupListViewerViewModel viewModel)
        {
            viewModel.OnItemChecked(item);
        }
    }

    private void OnDisplaySoflanItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SoflanGroupWrapItem item } &&
            DataContext is FumenSoflanGroupListViewerViewModel viewModel)
        {
            viewModel.OnDisplaySoflanItemChecked(item);
        }
    }

    private void OnSoflanPointDoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: SoflanPointRow row } &&
            DataContext is FumenSoflanGroupListViewerViewModel viewModel)
        {
            viewModel.NavigateToSoflanPointCommand.Execute(row);
        }
    }
}
