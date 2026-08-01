using System.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace OngekiFumenEditor.Avalonia.UI.Behaviors;

public enum DataGridRowDropPosition
{
    Before,
    Inside,
    After
}

public interface IDataGridRowReorderHandler<TItem> where TItem : class
{
    bool CanStartReorder(IReadOnlyList<TItem> items);

    bool CanReorder(
        IReadOnlyList<TItem> items,
        TItem target,
        DataGridRowDropPosition position);

    void Reorder(
        IReadOnlyList<TItem> items,
        TItem target,
        DataGridRowDropPosition position);
}

public static class DataGridRowReorderOperations
{
    public static IReadOnlyList<TItem> Reorder<TItem>(
        IReadOnlyList<TItem> source,
        IReadOnlyList<TItem> movingItems,
        TItem target,
        DataGridRowDropPosition position)
        where TItem : class
    {
        if (position == DataGridRowDropPosition.Inside)
            return source.ToArray();

        var movingSet = movingItems.ToHashSet();
        var orderedMovingItems = source.Where(movingSet.Contains).ToArray();
        if (orderedMovingItems.Length == 0)
            return source.ToArray();

        var targetIndex = source.ToList().IndexOf(target);
        if (targetIndex < 0)
            return source.ToArray();

        var insertionBoundary = targetIndex + (position == DataGridRowDropPosition.After ? 1 : 0);
        var removedBeforeBoundary = source
            .Take(insertionBoundary)
            .Count(movingSet.Contains);
        var insertionIndex = insertionBoundary - removedBeforeBoundary;

        var result = source.Where(x => !movingSet.Contains(x)).ToList();
        result.InsertRange(insertionIndex, orderedMovingItems);
        return result;
    }
}

public abstract class DataGridRowReorderBehaviorBase : Behavior<DataGrid>
{
    public static readonly StyledProperty<IBrush> IndicatorBrushProperty =
        AvaloniaProperty.Register<DataGridRowReorderBehaviorBase, IBrush>(
            nameof(IndicatorBrush), Brushes.DodgerBlue);

    public IBrush IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }
}

public abstract class DataGridRowReorderBehavior<TItem> : DataGridRowReorderBehaviorBase where TItem : class
{
    private const double MinimumDragDistance = 4;
    private const double AutoScrollEdgeSize = 40;
    private const double MaximumAutoScrollStep = 18;

    private static readonly DataFormat<string> RowReorderDataFormat =
        DataFormat.CreateStringApplicationFormat("OngekiFumenEditor.DataGridRowReorder");

    private readonly DispatcherTimer autoScrollTimer;
    private Point pointerPressedPosition;
    private TItem pointerPressedItem;
    private IReadOnlyList<TItem> draggingItems;
    private string activeDragToken;
    private bool isDragging;
    private double autoScrollStep;
    private ScrollViewer scrollViewer;
    private DataGridRow indicatorRow;

    protected DataGridRowReorderBehavior()
    {
        autoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        autoScrollTimer.Tick += OnAutoScrollTimerTick;
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();

        var grid = AssociatedObject;
        DragDrop.SetAllowDrop(grid, true);
        grid.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        grid.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, true);
        grid.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
        grid.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble, true);
        grid.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, RoutingStrategies.Bubble, true);
        grid.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble, true);
    }

    protected override void OnDetachedFromVisualTree()
    {
        var grid = AssociatedObject;
        grid.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        grid.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        grid.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        grid.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        grid.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        grid.RemoveHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(grid, false);

        ResetDragState();
        base.OnDetachedFromVisualTree();
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        ResetPointerState();

        var grid = AssociatedObject;
        if (!e.GetCurrentPoint(grid).Properties.IsLeftButtonPressed || e.Source is not Control source)
            return;

        var row = DataGridRow.GetRowContainingElement(source);
        if (row?.DataContext is not TItem item || IsInteractiveChild(source, row))
            return;

        pointerPressedPosition = e.GetPosition(grid);
        pointerPressedItem = item;
    }

    private async void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (isDragging || pointerPressedItem is null || !e.Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(AssociatedObject);
        var delta = position - pointerPressedPosition;
        if (Math.Abs(delta.X) < MinimumDragDistance && Math.Abs(delta.Y) < MinimumDragDistance)
            return;

        var items = GetItemsForDrag(pointerPressedItem);
        if (AssociatedObject.DataContext is not IDataGridRowReorderHandler<TItem> handler ||
            !handler.CanStartReorder(items))
        {
            ResetPointerState();
            return;
        }

        isDragging = true;
        draggingItems = items;
        activeDragToken = Guid.NewGuid().ToString("N");
        e.Handled = true;

        var dataTransfer = new DataTransfer();
        var dataTransferItem = new DataTransferItem();
        dataTransferItem.Set(RowReorderDataFormat, activeDragToken);
        dataTransfer.Add(dataTransferItem);

        try
        {
            await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Move);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (!isDragging)
            ResetPointerState();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!IsCurrentDrag(e))
            return;

        UpdateAutoScroll(e.GetPosition(AssociatedObject));
        if (TryResolveDrop(e, out var target, out var position, out var row))
        {
            SetInsertionIndicator(row, position);
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            ClearInsertionIndicator();
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (!IsCurrentDrag(e))
            return;

        var position = e.GetPosition(AssociatedObject);
        if (new Rect(AssociatedObject.Bounds.Size).Contains(position))
            return;

        StopAutoScroll();
        ClearInsertionIndicator();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!IsCurrentDrag(e))
            return;

        StopAutoScroll();
        ClearInsertionIndicator();

        if (TryResolveDrop(e, out var target, out var position, out _) &&
            AssociatedObject.DataContext is IDataGridRowReorderHandler<TItem> handler)
        {
            var movedItems = draggingItems.ToArray();
            handler.Reorder(movedItems, target, position);
            RestoreSelection(movedItems);
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private IReadOnlyList<TItem> GetItemsForDrag(TItem pressedItem)
    {
        var selectedItems = AssociatedObject.SelectedItems
            .OfType<TItem>()
            .ToHashSet();
        if (!selectedItems.Contains(pressedItem))
            return [pressedItem];

        return EnumerateItems()
            .Where(selectedItems.Contains)
            .ToArray();
    }

    private IEnumerable<TItem> EnumerateItems()
    {
        return AssociatedObject.ItemsSource is IEnumerable items
            ? items.OfType<TItem>()
            : [];
    }

    private bool TryResolveDrop(
        DragEventArgs e,
        out TItem target,
        out DataGridRowDropPosition position,
        out DataGridRow targetRow)
    {
        target = default;
        position = default;
        targetRow = e.Source is Control source
            ? DataGridRow.GetRowContainingElement(source)
            : null;

        if (targetRow?.DataContext is not TItem rowItem)
        {
            if (!TryFindNearestRealizedRow(e.GetPosition(AssociatedObject), out targetRow, out rowItem, out position))
                return false;
        }
        else
        {
            var rowPosition = e.GetPosition(targetRow);
            position = rowPosition.Y < targetRow.Bounds.Height / 2
                ? DataGridRowDropPosition.Before
                : DataGridRowDropPosition.After;
        }

        if (AssociatedObject.DataContext is not IDataGridRowReorderHandler<TItem> handler)
            return false;

        var candidatePositions = new[]
        {
            position,
            DataGridRowDropPosition.Inside
        };

        foreach (var candidate in candidatePositions)
        {
            if (!handler.CanReorder(draggingItems, rowItem, candidate))
                continue;

            target = rowItem;
            position = candidate;
            return true;
        }

        return false;
    }

    // DataGrid only realizes viewport rows. The target is therefore derived from realized
    // containers, while the view model computes the final index against the full source.
    private bool TryFindNearestRealizedRow(
        Point gridPosition,
        out DataGridRow targetRow,
        out TItem target,
        out DataGridRowDropPosition position)
    {
        targetRow = null;
        target = default;
        position = default;

        DataGridRow lastRow = null;
        TItem lastItem = default;
        foreach (var row in AssociatedObject.GetVisualDescendants()
                     .OfType<DataGridRow>()
                     .Where(x => x.IsVisible && x.DataContext is TItem)
                     .OrderBy(x => x.Index))
        {
            if (row.TranslatePoint(default, AssociatedObject) is not { } rowOrigin)
                continue;

            var item = (TItem)row.DataContext;
            if (gridPosition.Y < rowOrigin.Y + row.Bounds.Height / 2)
            {
                targetRow = row;
                target = item;
                position = DataGridRowDropPosition.Before;
                return true;
            }

            lastRow = row;
            lastItem = item;
        }

        if (lastRow is null)
            return false;

        targetRow = lastRow;
        target = lastItem;
        position = DataGridRowDropPosition.After;
        return true;
    }

    private void SetInsertionIndicator(DataGridRow row, DataGridRowDropPosition position)
    {
        ClearInsertionIndicator();

        var border = new Border
        {
            BorderBrush = IndicatorBrush,
            BorderThickness = position switch
            {
                DataGridRowDropPosition.Before => new Thickness(0, 2, 0, 0),
                DataGridRowDropPosition.After => new Thickness(0, 0, 0, 2),
                _ => new Thickness(2)
            },
            IsHitTestVisible = false
        };

        indicatorRow = row;
        AdornerLayer.SetAdorner(row, border);
    }

    private void ClearInsertionIndicator()
    {
        if (indicatorRow is null)
            return;

        AdornerLayer.SetAdorner(indicatorRow, null);
        indicatorRow = null;
    }

    private void UpdateAutoScroll(Point pointerPosition)
    {
        scrollViewer ??= AssociatedObject.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .OrderByDescending(x => x.Extent.Height - x.Viewport.Height)
            .FirstOrDefault(x => x.Extent.Height > x.Viewport.Height);

        if (scrollViewer is null || AssociatedObject.Bounds.Height <= 0)
        {
            StopAutoScroll();
            return;
        }

        var edgeSize = Math.Min(AutoScrollEdgeSize, AssociatedObject.Bounds.Height / 4);
        if (pointerPosition.Y < edgeSize)
        {
            autoScrollStep = -MaximumAutoScrollStep * (1 - Math.Max(0, pointerPosition.Y) / edgeSize);
        }
        else if (pointerPosition.Y > AssociatedObject.Bounds.Height - edgeSize)
        {
            var distanceFromBottom = Math.Max(0, AssociatedObject.Bounds.Height - pointerPosition.Y);
            autoScrollStep = MaximumAutoScrollStep * (1 - distanceFromBottom / edgeSize);
        }
        else
        {
            StopAutoScroll();
            return;
        }

        if (!autoScrollTimer.IsEnabled)
            autoScrollTimer.Start();
    }

    private void OnAutoScrollTimerTick(object sender, EventArgs e)
    {
        if (scrollViewer is null || Math.Abs(autoScrollStep) < double.Epsilon)
            return;

        var maximum = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var nextOffset = Math.Clamp(scrollViewer.Offset.Y + autoScrollStep, 0, maximum);
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffset);
    }

    private void StopAutoScroll()
    {
        autoScrollStep = 0;
        autoScrollTimer.Stop();
    }

    private bool IsCurrentDrag(DragEventArgs e)
    {
        return isDragging &&
               activeDragToken is not null &&
               e.DataTransfer.TryGetValue(RowReorderDataFormat) == activeDragToken;
    }

    private void RestoreSelection(IReadOnlyList<TItem> items)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (items.Count == 0)
                return;

            var availableItems = EnumerateItems().ToHashSet();
            var selectedItems = items.Where(availableItems.Contains).ToArray();
            if (selectedItems.Length == 0)
                return;

            AssociatedObject.SelectedItem = selectedItems[0];
            foreach (var item in selectedItems.Skip(1))
            {
                if (!AssociatedObject.SelectedItems.Contains(item))
                    AssociatedObject.SelectedItems.Add(item);
            }
        });
    }

    private void ResetPointerState()
    {
        pointerPressedItem = null;
        pointerPressedPosition = default;
    }

    private void ResetDragState()
    {
        StopAutoScroll();
        ClearInsertionIndicator();
        ResetPointerState();
        draggingItems = null;
        activeDragToken = null;
        scrollViewer = null;
        isDragging = false;
    }

    private static bool IsInteractiveChild(Control source, DataGridRow row)
    {
        if (source is Button or TextBox or ComboBox)
            return true;

        return source.GetVisualAncestors()
            .TakeWhile(x => x != row)
            .Any(x => x is Button or TextBox or ComboBox);
    }
}
