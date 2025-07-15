using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using OngekiFumenEditor.Modules.SelectionFilter.ViewModels;
using OngekiFumenEditor.Utils;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.Toolkit;

namespace OngekiFumenEditor.Modules.SelectionFilter.Views;

public partial class SelectionFilterWindowView : Window
{
    public SelectionFilterWindowView()
    {
        InitializeComponent();

        FilterDockableTypeCheckComboBox.Opened += CheckComboBox_FixAllSelect;
        FilterDockableTypeCheckComboBox.Closed += CheckComboBox_SetText;
        FilterDockableTypeCheckComboBox.Loaded += CheckComboBox_SetText;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CheckComboBox_SetText(object sender, EventArgs e)
    {
        var items = ((CheckComboBox)sender).Items.Cast<FilterDockableLaneOption>().ToArray();
        if (items.All(o => o.IsSelected) || items.All(o => !o.IsSelected)) {
            FilterDockableTypeCheckComboBox.Text = Properties.Resources.SelectionFilter_Any;
        }
    }

    private void CheckComboBox_FixAllSelect(object sender, EventArgs e)
    {
        var items = ((CheckComboBox)sender).Items.Cast<FilterDockableLaneOption>().ToArray();
        if (items.All(o => o.IsSelected)) {
            FilterDockableTypeCheckComboBox.SelectAll();
        }
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
        var selectedItems = comboBox.Items.Cast<FilterBulletPalettesItem>().Where(i => i.IsSelected).ToArray();
        if (selectedItems.Length == 0 || selectedItems.Length == comboBox.Items.Count) {
            comboBox.Text = Properties.Resources.SelectionFilter_Any;
        }
        else {
            comboBox.Text =
                Properties.Resources.SelectionFilter_ComboLabelBulletPaletteSelectCount.Format(selectedItems.Length);
        }
    }
}