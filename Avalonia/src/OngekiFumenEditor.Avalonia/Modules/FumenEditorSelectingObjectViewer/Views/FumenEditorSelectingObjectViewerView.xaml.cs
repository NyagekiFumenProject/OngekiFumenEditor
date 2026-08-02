using Avalonia.Controls;
using Avalonia.Input;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Views;

public partial class FumenEditorSelectingObjectViewerView : ViewBase
{
    public FumenEditorSelectingObjectViewerView()
    {
        InitializeComponent();
    }

    private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { DataContext: SelectedObjectRow row } &&
            DataContext is FumenEditorSelectingObjectViewerViewModel viewModel)
        {
            viewModel.OnItemSingleClick(row.Object);
        }
    }

    private void OnRowDoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: SelectedObjectRow row } &&
            DataContext is FumenEditorSelectingObjectViewerViewModel viewModel)
        {
            viewModel.FocusItemCommand.Execute(row.Object);
        }
    }
}
