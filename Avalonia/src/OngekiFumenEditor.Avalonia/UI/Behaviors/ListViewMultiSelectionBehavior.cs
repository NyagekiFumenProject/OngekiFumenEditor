using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace OngekiFumenEditor.Avalonia.UI.Behaviors;

/// <summary>
/// 把 DataGrid 的多选选中项与 ViewModel 的 SelectedItems（IList）双向同步。
/// 对应原 WPF ListView 版本：VM 集合变化时更新 DataGrid 选中项，
/// 用户在 DataGrid 上改变选择时写回 VM 集合。
/// </summary>
public class ListViewMultiSelectionBehavior : Behavior<DataGrid>
{
    public static readonly StyledProperty<IList> SelectedItemsProperty =
        AvaloniaProperty.Register<ListViewMultiSelectionBehavior, IList>(nameof(SelectedItems));

    private bool isUpdatingTarget;
    private bool isUpdatingSource;
    private INotifyCollectionChanged subscribedSource;

    public IList SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        if (AssociatedObject is null)
            return;

        SyncTargetFromSource();
        AssociatedObject.SelectionChanged += DataGridSelectionChanged;
        SubscribeSource(SelectedItems as INotifyCollectionChanged);
    }

    protected override void OnDetachedFromVisualTree()
    {
        if (AssociatedObject is not null)
            AssociatedObject.SelectionChanged -= DataGridSelectionChanged;
        SubscribeSource(null);
        base.OnDetachedFromVisualTree();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SelectedItemsProperty)
            return;

        if (AssociatedObject is not null)
        {
            SubscribeSource(change.NewValue as INotifyCollectionChanged);
            SyncTargetFromSource();
        }
    }

    private void SubscribeSource(INotifyCollectionChanged newSource)
    {
        if (subscribedSource is not null)
            subscribedSource.CollectionChanged -= SourceCollectionChanged;
        subscribedSource = newSource;
        if (subscribedSource is not null)
            subscribedSource.CollectionChanged += SourceCollectionChanged;
    }

    // VM 集合 -> DataGrid 选中项。
    private void SyncTargetFromSource()
    {
        if (AssociatedObject is null || isUpdatingSource)
            return;

        try
        {
            isUpdatingTarget = true;
            var selected = AssociatedObject.SelectedItems;
            selected.Clear();
            if (SelectedItems is not null)
            {
                foreach (var item in SelectedItems)
                    selected.Add(item);
            }
        }
        finally
        {
            isUpdatingTarget = false;
        }
    }

    private void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (isUpdatingSource || AssociatedObject is null)
            return;

        try
        {
            isUpdatingTarget = true;
            var selected = AssociatedObject.SelectedItems;

            if (e.Action == NotifyCollectionChangedAction.Reset)
                selected.Clear();

            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                    selected.Remove(item);
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                    selected.Add(item);
            }
        }
        finally
        {
            isUpdatingTarget = false;
        }
    }

    // DataGrid 选中项 -> VM 集合。
    private void DataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingTarget)
            return;

        var selectedItems = SelectedItems;
        if (selectedItems is null)
            return;

        try
        {
            isUpdatingSource = true;

            foreach (var item in e.RemovedItems)
                selectedItems.Remove(item);

            foreach (var item in e.AddedItems)
                selectedItems.Add(item);
        }
        finally
        {
            isUpdatingSource = false;
        }
    }
}
