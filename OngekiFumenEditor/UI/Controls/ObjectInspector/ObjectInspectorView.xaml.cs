using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector
{
    /// <summary>
    /// FumenMetaInfoBrowserView.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectInspectorView : UserControl
    {
        public object InspectObject
        {
            get { return GetValue(InspectObjectProperty); }
            set { SetValue(InspectObjectProperty, value); }
        }

        public ItemsPanelTemplate ItemsPanel
        {
            get { return (ItemsPanelTemplate)GetValue(ItemsPanelProperty); }
            set { SetValue(ItemsPanelProperty, value); }
        }

        public static readonly DependencyProperty InspectObjectProperty =
            DependencyProperty.Register("InspectObject", typeof(object), typeof(ObjectInspectorView), new PropertyMetadata(null, (s, e) =>
            {
                if (s is ObjectInspectorView ow && ow.HostContent.DataContext is ObjectInspectorViewModel om)
                {
                    om.InspectObject = e.NewValue;
                }
            }));

        public static readonly DependencyProperty ItemsPanelProperty =
            DependencyProperty.Register("ItemsPanel", typeof(ItemsPanelTemplate), typeof(ObjectInspectorView), new PropertyMetadata(null, (s, e) =>
            {
                if (s is ObjectInspectorView ow && e.NewValue is ItemsPanelTemplate newValue)
                    ow.itemsControl.ItemsPanel = newValue;
            }));

        public ObjectInspectorView()
        {
            InitializeComponent();

            HostContent.DataContext = new ObjectInspectorViewModel();
        }
    }
}
