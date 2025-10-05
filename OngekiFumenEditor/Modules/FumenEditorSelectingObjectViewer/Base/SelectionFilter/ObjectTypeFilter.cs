using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;

public sealed class FilterObjectTypeCategory : PropertyChangedBase
{
    public ObservableCollection<FilterObjectTypesItem> Items { get; private set; } = new();

    private readonly string CategoryName;

    public string CategoryNameDisplay
    {
        get;
        private set => Set(ref field, value);
    }

    public string CategoryNameDisplayCheckCount
    {
        get;
        private set => Set(ref field, value);
    }

    public FilterObjectTypeCategory(SelectionFilterViewModel filter, string categoryName, IEnumerable<FilterObjectTypesItem> items)
    {
        CategoryName = categoryName;

        Items.CollectionChanged += (_, args) =>
        {
            if (args.NewItems?.Count > 0) {
                foreach (var item in args.NewItems.Cast<FilterObjectTypesItem>()) {
                    item.PropertyChanged += (_, typeArgs) =>
                    {
                        if (typeArgs.PropertyName == nameof(FilterObjectTypesItem.IsSelected)) {
                            filter.OnTypeFilterEnabledChanged(item);
                            UpdateCategoryNameDisplay();
                        }
                    };
                }
            }
        };

        Items.AddRange(items);
        UpdateCategoryNameDisplay();
    }

    public void UpdateCategoryNameDisplay()
    {
        var matches = Items.Sum(i => i.MatchingObjects.Count);
        CategoryNameDisplay = $"{CategoryName} ({matches})";
        CategoryNameDisplayCheckCount = $"{CategoryName} ({Items.Count(i => i.IsSelected)} / {Items.Count})";
    }
}

public class FilterObjectTypesItem : PropertyChangedBase
{
    public required string Text { get; init; }
    public required Type[] Types { get; init; }

    public ObservableCollection<ISelectableObject> MatchingObjects { get; } = new();

    public FilterObjectTypesItem()
    {
        MatchingObjects.CollectionChanged += (_, _) => NotifyOfPropertyChange(nameof(Display));
    }

    public string Display => $"{Text} ({MatchingObjects.Count})";

    public bool IsSelected
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOfPropertyChange(nameof(Display));
        }
    } = false;
}

