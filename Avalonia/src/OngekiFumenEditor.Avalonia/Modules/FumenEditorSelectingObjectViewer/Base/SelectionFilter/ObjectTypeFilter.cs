using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;

public sealed class FilterObjectTypeCategory : ObservableObject
{
    public ObservableCollection<FilterObjectTypesItem> Items { get; } = [];

    private readonly string categoryName;

    public string CategoryNameDisplay
    {
        get => field;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string CategoryNameDisplayCheckCount
    {
        get => field;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public FilterObjectTypeCategory(SelectionFilterViewModel filter, string categoryName, IEnumerable<FilterObjectTypesItem> items)
    {
        this.categoryName = categoryName;

        Items.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
            {
                foreach (var item in args.NewItems.Cast<FilterObjectTypesItem>())
                {
                    item.PropertyChanged += (_, typeArgs) =>
                    {
                        if (typeArgs.PropertyName == nameof(FilterObjectTypesItem.IsSelected))
                        {
                            filter.OnTypeFilterEnabledChanged(item);
                            UpdateCategoryNameDisplay();
                            OnPropertyChanged(nameof(IsAllSelected));
                        }
                    };
                }
            }
        };

        foreach (var item in items)
            Items.Add(item);

        UpdateCategoryNameDisplay();
    }

    public void UpdateCategoryNameDisplay()
    {
        var matches = Items.Sum(i => i.MatchingObjects.Count);
        CategoryNameDisplay = $"{categoryName} ({matches})";
        CategoryNameDisplayCheckCount = $"{categoryName} ({Items.Count(i => i.IsSelected)} / {Items.Count})";
    }

    public bool IsAllSelected
    {
        get => Items.Count > 0 && Items.All(item => item.IsSelected);
        set
        {
            foreach (var item in Items)
                item.IsSelected = value;

            OnPropertyChanged();
        }
    }
}

public class FilterObjectTypesItem : ObservableObject
{
    public required string Text { get; init; }
    public required Type[] Types { get; init; }

    public ObservableCollection<ISelectableObject> MatchingObjects { get; } = [];

    public FilterObjectTypesItem()
    {
        MatchingObjects.CollectionChanged += OnMatchingObjectsCollectionChanged;
    }

    private void OnMatchingObjectsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Display)));
    }

    public string Display => $"{Text} ({MatchingObjects.Count})";

    public bool IsSelected
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Display)));
        }
    }
}
