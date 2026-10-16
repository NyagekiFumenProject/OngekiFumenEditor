using OngekiFumenEditor.Properties;
using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public static class SoflanGroupSelector
    {
        public static UIElement Create(IObjectPropertyAccessProxy wrapper)
        {
            var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
            var selector = new ComboBox { IsEditable = false, MinWidth = 100,
                DisplayMemberPath = nameof(SoflanGroupWrapItem.SelectionDisplayName),
                SelectedValuePath = nameof(SoflanGroupWrapItem.SoflanGroupId),
                ToolTip = Resources.SoflanGroupSelectorTip };
            bool refreshing = false;
            void Refresh()
            {
                refreshing = true;
                selector.ItemsSource = editor?.Fumen?.IndividualSoflanAreaMap.GetSoflanGroups().ToArray();
                selector.SelectedValue = wrapper.ProxyValue;
                selector.IsEnabled = !wrapper.IsReadOnly && editor?.Fumen != null;
                refreshing = false;
            }
            selector.DropDownOpened += (_, _) => Refresh();
            selector.SelectionChanged += (_, _) =>
            {
                if (!refreshing && selector.SelectedItem is SoflanGroupWrapItem item &&
                    editor.Fumen.IndividualSoflanAreaMap.GetSoflanGroups().Contains(item) &&
                    !Equals(wrapper.ProxyValue, item.SoflanGroupId))
                    wrapper.ProxyValue = item.SoflanGroupId;
            };
            System.ComponentModel.PropertyChangedEventHandler changed = (_, _) => Refresh();
            selector.Loaded += (_, _) => { wrapper.PropertyChanged += changed; Refresh(); };
            selector.Unloaded += (_, _) => wrapper.PropertyChanged -= changed;
            Refresh();
            return selector;
        }
    }
}
