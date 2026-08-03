using Gekimini.Avalonia.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.DragDrops;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;

public partial class HoldOperationView : ViewBase
{
    private readonly PointerDragSession dragSession = new();

    public HoldOperationView()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        dragSession.Arm(e);
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (!dragSession.TryConsume(e) || DataContext is not HoldOperationViewModel viewModel)
            return;

        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(
            e,
            viewModel.CreateHoldEndDropAction(),
            DragDropEffects.Move);
    }
}
