using Gekimini.Avalonia.Views;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.DragDrops;
using Gekimini.Avalonia.Framework.DragDrops.Behaviors;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;

public partial class ConnectableObjectOperationView : ViewBase
{
    private readonly PointerDragSession dragSession = new();

    public ConnectableObjectOperationView()
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

    private void OnSplitPointerMoved(object sender, PointerEventArgs e)
    {
        TryStartDrag(e, ConnectableObjectOperationViewModel.DragActionType.Split);
    }

    private void OnCurvePathControlPointerMoved(object sender, PointerEventArgs e)
    {
        TryStartDrag(e, ConnectableObjectOperationViewModel.DragActionType.DropCurvePathControl);
    }

    private void TryStartDrag(PointerEventArgs e, ConnectableObjectOperationViewModel.DragActionType actionType)
    {
        if (!dragSession.TryConsume(e, out var triggerEvent) ||
            DataContext is not ConnectableObjectOperationViewModel viewModel ||
            viewModel.CreateDropAction(actionType) is not { } dropAction)
        {
            return;
        }

        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(triggerEvent, dropAction, DragDropEffects.Move);
    }
}

internal sealed class PointerDragSession
{
    private bool isArmed;
    private Point startPosition;
    private PointerPressedEventArgs triggerEvent;

    public void Arm(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            isArmed = false;
            triggerEvent = null;
            return;
        }

        startPosition = e.GetPosition(null);
        triggerEvent = e;
        isArmed = true;
    }

    public bool TryConsume(PointerEventArgs e, out PointerPressedEventArgs pressedEvent)
    {
        pressedEvent = null;
        if (!isArmed)
            return false;

        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            isArmed = false;
            triggerEvent = null;
            return false;
        }

        var difference = startPosition - e.GetPosition(null);
        if (Math.Abs(difference.X) <= DragDataContextOutBehavior.MinimumHorizontalDragDistance &&
            Math.Abs(difference.Y) <= DragDataContextOutBehavior.MinimumVerticalDragDistance)
        {
            return false;
        }

        pressedEvent = triggerEvent;
        triggerEvent = null;
        isArmed = false;
        return pressedEvent is not null;
    }
}
