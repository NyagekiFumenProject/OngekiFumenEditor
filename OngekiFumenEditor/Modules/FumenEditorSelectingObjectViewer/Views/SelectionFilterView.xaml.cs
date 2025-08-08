using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MahApps.Metro.Controls;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Utils;
using Xceed.Wpf.Toolkit;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Views;

public partial class SelectionFilterView : UserControl
{
    public SelectionFilterView()
    {
        InitializeComponent();
    }

    private void FixFilterTypesAllSelect(object sender, EventArgs e)
    {
        var listBox = (CheckListBox)sender;
        var items = listBox.Items.Cast<FilterObjectTypesItem>().ToArray();

        // Stupid hack to refresh the "select all" button for partial selections.
        items[0].IsSelected = !items[0].IsSelected;
        items[0].IsSelected = !items[0].IsSelected;

        // Properly check the "Select All" option when all types exist in the selection
        if (items.All(o => o.IsSelected)) {
            listBox.UnSelectAll();
            listBox.SelectAll();
        }
    }

    private void BulletPaletteCheckList_SetText(object sender, EventArgs e)
    {
        var comboBox = (CheckComboBox)sender;
        if (comboBox.SelectedItems.Count == 0) {
            comboBox.Text = Properties.Resources.SelectionFilter_None;
        } else if (comboBox.SelectedItems.Count == comboBox.Items.Count) {
            comboBox.Text = Properties.Resources.SelectionFilter_Any;
        }
        else {
            comboBox.Text =
                Properties.Resources.SelectionFilter_ComboLabelBulletPaletteSelectCount.Format(comboBox.SelectedItems.Count);
        }
    }

    private void BubbleMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
        eventArg.RoutedEvent = MouseWheelEvent;
        eventArg.Source = sender;
        var parent = (UIElement)((Control)sender).Parent;
        parent?.RaiseEvent(eventArg);
    }
}