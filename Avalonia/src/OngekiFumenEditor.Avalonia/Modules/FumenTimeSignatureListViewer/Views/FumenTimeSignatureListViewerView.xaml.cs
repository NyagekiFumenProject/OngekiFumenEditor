using Avalonia.Controls;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Views;

public partial class FumenTimeSignatureListViewerView : UserControl
{
    public FumenTimeSignatureListViewerView()
    {
        InitializeComponent();
    }

    private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { DataContext: DisplayTimeSignatureItem item } &&
            DataContext is FumenTimeSignatureListViewerViewModel viewModel)
        {
            viewModel.OnItemSingleClick(item);
        }
    }
}
