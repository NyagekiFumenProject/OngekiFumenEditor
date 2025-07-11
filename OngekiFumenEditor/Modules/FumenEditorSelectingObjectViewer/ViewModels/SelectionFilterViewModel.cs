using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Kernel.EditorLayout;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using Binding = System.Windows.Data.Binding;
using ListBox = System.Windows.Controls.ListBox;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;

public class SelectionFilterViewModel : PropertyChangedBase
{
    public FumenVisualEditorViewModel Editor { get; set; }

    public ObservableCollection<ISelectableObject> FilteredObjects { get; set; } = new();
    public ObservableCollection<ISelectableObject> ObjectsToDeselect { get; set; } = new();

    private FilterMode _filterSelectionChangeMode = FilterMode.Remove;
    public FilterMode FilterSelectionChangeMode
    {
        get => _filterSelectionChangeMode;
        set
        {
            Set(ref _filterSelectionChangeMode, value);
            FilterModeChanged();
        }
    }

    #region Filter Categories

    public ObservableCollection<FilterObjectTypesItem> FilterItemsLanes { get; set; } = new()
    {
        new FilterObjectTypesItem() { IsCategoryHeader = true, Text = "All Lane Objects", Types = [typeof(ConnectableObjectBase)] },
        new FilterObjectTypesItem() { Text = "Wall Left", Types = [typeof(WallLeftNext), typeof(WallLeftStart)] },
        new FilterObjectTypesItem() { Text = "Lane Left", Types = [typeof(LaneLeftNext), typeof(LaneLeftStart)] },
        new FilterObjectTypesItem() { Text = "Lane Center", Types = [typeof(LaneCenterNext), typeof(LaneCenterStart)] },
        new FilterObjectTypesItem() { Text = "Lane Right", Types = [typeof(LaneRightNext), typeof(LaneRightStart)] },
        new FilterObjectTypesItem() { Text = "Wall Right", Types = [typeof(WallRightNext), typeof(WallRightStart)] },
        new FilterObjectTypesItem() { Text = "Lane Colorful", Types = [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)] },
        new FilterObjectTypesItem() { Text = "AutoPlayFaderLane", Types = [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)] },
        new FilterObjectTypesItem() { Text = "Beam", Types = [typeof(BeamNext), typeof(BeamStart)] },
        new FilterObjectTypesItem() { Text = "Curve Controls", Types = [typeof(LaneCurvePathControlObject)] }
    };

    public ObservableCollection<FilterObjectTypesItem> FilterItemsDockables { get; set; } = new()
    {
        new FilterObjectTypesItem() { IsCategoryHeader = true, Text = "All Dockable Objects", Types = [typeof(ILaneDockable)] },
        new FilterObjectTypesItem() { Text = "Tap", Types = [typeof(Tap)] },
        new FilterObjectTypesItem() { Text = "Hold", Types = [typeof(Hold), typeof(HoldEnd)] },
    };

    public ObservableCollection<FilterObjectTypesItem> FilterItemsFloatings { get; private set; } = new()
    {
        new() { IsCategoryHeader = true, Text = "All Floating Objects", Types = [typeof(Bell), typeof(Bullet), typeof(Flick)] },
        new() { Text = "Bell", Types = [typeof(Bell)] },
        new() { Text = "Bullet", Types = [typeof(Bullet)] },
        new() { Text = "Flick", Types = [typeof(Flick)] }
    };

    #endregion

    public SelectionFilterViewModel()
    {
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    public void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilteredObjects.Clear();
        FilteredObjects.AddRange(GetFilterMatches());

        var categoryHeader = ((ObservableCollection<FilterObjectTypesItem>)((ListBox)sender).ItemsSource).Single(i => i.IsCategoryHeader);
        var itemsInCategory = ((ObservableCollection<FilterObjectTypesItem>)((ListBox)sender).ItemsSource).Where(i => !i.IsCategoryHeader).ToList();

        if (e.AddedItems.Contains(categoryHeader)) {
            itemsInCategory.ForEach(i => i.IsSelected = true);
        } else if (e.RemovedItems.Contains(categoryHeader)) {
            itemsInCategory.ForEach(i => i.IsSelected = false);
        }

        UpdateObjectsToDeselect();
    }

    private void FilterModeChanged()
    {
        UpdateObjectsToDeselect();
    }

    private void UpdateObjectsToDeselect()
    {
        ObjectsToDeselect.Clear();
        switch (FilterSelectionChangeMode) {
            case FilterMode.Remove:
                ObjectsToDeselect.AddRange(FilteredObjects);
                break;
            case FilterMode.Replace:
                ObjectsToDeselect.AddRange(Editor.SelectObjects.Except(FilteredObjects));
                break;
        }
    }

    private IEnumerable<ISelectableObject> GetFilterMatches()
    {
        var filterTypes = GetFilteredTypes().ToList();
        return Editor.SelectObjects.Where(o => filterTypes.Any(t => t.IsInstanceOfType(o)));
    }

    private IEnumerable<Type> GetFilteredTypes()
    {
        return FilterItemsLanes.Concat(FilterItemsDockables).Concat(FilterItemsFloatings)
            .Where(i => i.IsSelected)
            .SelectMany(o => o.Types);
    }
}


public enum FilterMode
{
    Remove,
    Replace
}

public class FilterObjectTypesItem : PropertyChangedBase
{
    public string Text { get; init; }
    public Type[] Types { get; init; }
    public bool IsCategoryHeader { get; init; }

    private bool _isSelected = false;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public class SelectedObjectsToLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not IEnumerable<ISelectableObject> objectsToDeselect
            || values[1] is not FilterMode
            || values[2] is not IEnumerable<ISelectableObject> selectedObjects)
            throw new InvalidOperationException();

        var deselectCount = objectsToDeselect.Count();
        var currentSelection = selectedObjects.Count();

        return $"{currentSelection} → {currentSelection - deselectCount} (-{deselectCount})";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FilterSelectionChangeModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == parameter;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(true) == true ? parameter : Binding.DoNothing;
    }
}
