using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace OngekiFumenEditor.Avalonia.UI.Controls;

/// <summary>
/// Xceed CheckComboBox 的轻量替代：DropDownButton + Flyout 内嵌 CheckListBox。
/// 按钮 Content 显示已勾选项的 DisplayMemberPath 摘要（", " 分隔）。
/// Xceed 的 ValueMemberPath 语义不实现。
/// </summary>
public class CheckComboBox : DropDownButton
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CheckComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string?> SelectedMemberPathProperty =
        AvaloniaProperty.Register<CheckComboBox, string?>(nameof(SelectedMemberPath));

    public static readonly StyledProperty<string?> DisplayMemberPathProperty =
        AvaloniaProperty.Register<CheckComboBox, string?>(nameof(DisplayMemberPath));

    public static readonly StyledProperty<bool> IsSelectAllActiveProperty =
        AvaloniaProperty.Register<CheckComboBox, bool>(nameof(IsSelectAllActive));

    private readonly CheckListBox innerList;
    private readonly Dictionary<Type, PropertyInfo?> selectedPropertyCache = new();
    private readonly Dictionary<Type, PropertyInfo?> displayPropertyCache = new();

    public CheckComboBox()
    {
        innerList = new CheckListBox
        {
            MaxHeight = 300,
        };
        innerList[!CheckListBox.ItemsSourceProperty] = this[!ItemsSourceProperty];
        innerList[!CheckListBox.SelectedMemberPathProperty] = this[!SelectedMemberPathProperty];
        innerList[!CheckListBox.DisplayMemberPathProperty] = this[!DisplayMemberPathProperty];
        innerList[!CheckListBox.IsSelectAllActiveProperty] = this[!IsSelectAllActiveProperty];

        var flyout = new Flyout { Content = innerList };
        flyout.Opened += (_, _) => innerList.MinWidth = Bounds.Width;
        flyout.Closed += (_, _) => UpdateSummaryText();
        Flyout = flyout;

        Content = "...";
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSummaryText();
    }

    private void UpdateSummaryText()
    {
        var selectedPath = SelectedMemberPath;
        var displayPath = DisplayMemberPath;
        if (string.IsNullOrEmpty(selectedPath) || string.IsNullOrEmpty(displayPath) || ItemsSource is null)
            return;

        var sb = new StringBuilder();
        foreach (var item in ItemsSource)
        {
            if (item is null)
                continue;
            var type = item.GetType();
            var selectedProp = GetCachedProperty(selectedPropertyCache, type, selectedPath);
            if (selectedProp?.GetValue(item) is not true)
                continue;
            var displayProp = GetCachedProperty(displayPropertyCache, type, displayPath);
            var text = displayProp?.GetValue(item)?.ToString();
            if (string.IsNullOrEmpty(text))
                continue;
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(text);
        }

        Content = sb.Length > 0 ? sb.ToString() : "...";
    }

    private static PropertyInfo? GetCachedProperty(Dictionary<Type, PropertyInfo?> cache, Type type, string path)
    {
        if (!cache.TryGetValue(type, out var prop))
        {
            prop = type.GetProperty(path, BindingFlags.Public | BindingFlags.Instance);
            cache[type] = prop;
        }

        return prop;
    }
}
