using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.DragDrops;
using Gekimini.Avalonia.Framework.DragDrops.Behaviors;
using OngekiFumenEditor.Avalonia.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;

public partial class ConnectableObjectOperationView : UserControl
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
        if (!dragSession.TryConsume(e) ||
            DataContext is not ConnectableObjectOperationViewModel viewModel ||
            viewModel.CreateDropAction(actionType) is not { } dropAction)
        {
            return;
        }

        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(e, dropAction, DragDropEffects.Move);
    }
}

internal sealed class PointerDragSession
{
    private bool isArmed;
    private Point startPosition;

    public void Arm(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            isArmed = false;
            return;
        }

        startPosition = e.GetPosition(null);
        isArmed = true;
    }

    public bool TryConsume(PointerEventArgs e)
    {
        if (!isArmed)
            return false;

        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            isArmed = false;
            return false;
        }

        var difference = startPosition - e.GetPosition(null);
        if (Math.Abs(difference.X) <= DragDataContextOutBehavior.MinimumHorizontalDragDistance &&
            Math.Abs(difference.Y) <= DragDataContextOutBehavior.MinimumVerticalDragDistance)
        {
            return false;
        }

        isArmed = false;
        return true;
    }
}
