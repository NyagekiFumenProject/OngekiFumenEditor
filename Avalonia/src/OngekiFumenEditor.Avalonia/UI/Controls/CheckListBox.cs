using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace OngekiFumenEditor.Avalonia.UI.Controls;

/// <summary>
/// Xceed CheckListBox 的轻量替代：ListBox + 每行 CheckBox，样式见 UI/Themes/CheckListBox.axaml。
/// </summary>
public class CheckListBox : ListBox
{
    public static readonly StyledProperty<string?> SelectedMemberPathProperty =
        AvaloniaProperty.Register<CheckListBox, string?>(nameof(SelectedMemberPath));

    public static readonly StyledProperty<string?> DisplayMemberPathProperty =
        AvaloniaProperty.Register<CheckListBox, string?>(nameof(DisplayMemberPath));

    public static readonly StyledProperty<bool> IsSelectAllActiveProperty =
        AvaloniaProperty.Register<CheckListBox, bool>(nameof(IsSelectAllActive));

    public static readonly StyledProperty<object?> SelectAllContentProperty =
        AvaloniaProperty.Register<CheckListBox, object?>(nameof(SelectAllContent));

    private readonly Dictionary<Type, PropertyInfo?> selectedPropertyCache = new();
    private CheckBox? selectAllCheckBox;

    static CheckListBox()
    {
        SelectedMemberPathProperty.Changed.AddClassHandler<CheckListBox>((x, _) => x.UpdateItemTemplate());
        DisplayMemberPathProperty.Changed.AddClassHandler<CheckListBox>((x, _) => x.UpdateItemTemplate());
    }

    protected override Type StyleKeyOverride => typeof(CheckListBox);

    public string? SelectedMemberPath
    {
        get => GetValue(SelectedMemberPathProperty);
        set => SetValue(SelectedMemberPathProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public bool IsSelectAllActive
    {
        get => GetValue(IsSelectAllActiveProperty);
        set => SetValue(IsSelectAllActiveProperty, value);
    }

    public object? SelectAllContent
    {
        get => GetValue(SelectAllContentProperty);
        set => SetValue(SelectAllContentProperty, value);
    }

    private void UpdateItemTemplate()
    {
        var selectedPath = SelectedMemberPath;
        var displayPath = DisplayMemberPath;
        if (string.IsNullOrEmpty(selectedPath) && string.IsNullOrEmpty(displayPath))
        {
            ItemTemplate = null;
            return;
        }

        ItemTemplate = new FuncDataTemplate<object>((_, _) =>
        {
            var checkBox = new CheckBox();
            if (!string.IsNullOrEmpty(selectedPath))
                checkBox.Bind(CheckBox.IsCheckedProperty, new Binding(selectedPath) { Mode = BindingMode.TwoWay });
            if (!string.IsNullOrEmpty(displayPath))
                checkBox.Bind(ContentControl.ContentProperty, new Binding(displayPath));
            return checkBox;
        }, supportsRecycling: false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (selectAllCheckBox is not null)
            selectAllCheckBox.IsCheckedChanged -= OnSelectAllCheckedChanged;

        base.OnApplyTemplate(e);

        selectAllCheckBox = e.NameScope.Find<CheckBox>("PART_SelectAll");
        if (selectAllCheckBox is not null)
            selectAllCheckBox.IsCheckedChanged += OnSelectAllCheckedChanged;
    }

    private void OnSelectAllCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (selectAllCheckBox?.IsChecked is not { } isChecked)
            return;

        var path = SelectedMemberPath;
        if (string.IsNullOrEmpty(path) || ItemsSource is null)
            return;

        foreach (var item in ItemsSource)
        {
            if (item is null)
                continue;
            var prop = GetSelectedProperty(item.GetType(), path);
            if (prop?.CanWrite == true)
                prop.SetValue(item, isChecked);
        }
    }

    private PropertyInfo? GetSelectedProperty(Type type, string path)
    {
        if (!selectedPropertyCache.TryGetValue(type, out var prop))
        {
            prop = type.GetProperty(path, BindingFlags.Public | BindingFlags.Instance);
            selectedPropertyCache[type] = prop;
        }

        return prop;
    }
}
