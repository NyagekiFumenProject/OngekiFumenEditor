using Avalonia.Controls;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.DragDrops;
using OngekiFumenEditor.Avalonia.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;

public partial class WallOperationView : UserControl
{
    private readonly PointerDragSession dragSession = new();

    public WallOperationView()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        dragSession.Arm(e);
    }

    private void OnNextPointerMoved(object sender, PointerEventArgs e)
    {
        TryStartDrag(e, ConnectableObjectOperationViewModel.DragActionType.DropNext);
    }

    private void OnEndPointerMoved(object sender, PointerEventArgs e)
    {
        TryStartDrag(e, ConnectableObjectOperationViewModel.DragActionType.DropEnd);
    }

    private void TryStartDrag(PointerEventArgs e, ConnectableObjectOperationViewModel.DragActionType actionType)
    {
        if (!dragSession.TryConsume(e) ||
            DataContext is not WallOperationViewModel viewModel ||
            viewModel.CreateDropAction(actionType) is not { } dropAction)
        {
            return;
        }

        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(e, dropAction, DragDropEffects.Move);
    }
}
