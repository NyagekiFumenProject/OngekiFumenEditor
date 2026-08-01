using Avalonia.Controls;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.Views;

public partial class FumenBulletPalleteListViewerView : UserControl
{
    public FumenBulletPalleteListViewerView()
    {
        InitializeComponent();
    }

    private void OnCreateBulletPointerMoved(object sender, PointerEventArgs e)
    {
        if (DataContext is FumenBulletPalleteListViewerViewModel viewModel)
            viewModel.OnCreateBulletPointerMoved(e);
    }

    private void OnCreateBellPointerMoved(object sender, PointerEventArgs e)
    {
        if (DataContext is FumenBulletPalleteListViewerViewModel viewModel)
            viewModel.OnCreateBellPointerMoved(e);
    }

    private void OnCreateObjectPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is Control source && DataContext is FumenBulletPalleteListViewerViewModel viewModel)
            viewModel.OnCreateObjectPointerPressed(source, e);
    }
}
