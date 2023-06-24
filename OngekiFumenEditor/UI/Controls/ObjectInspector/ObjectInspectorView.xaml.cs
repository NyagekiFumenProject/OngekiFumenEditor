using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls.DefaultWaveformDrawing;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector
{
    /// <summary>
    /// FumenMetaInfoBrowserView.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectInspectorView : UserControl, INotifyPropertyChanged
    {
        public object InspectObject
        {
            get { return GetValue(InspectObjectProperty); }
            set { SetValue(InspectObjectProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InspectObject.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InspectObjectProperty =
            DependencyProperty.Register("InspectObject", typeof(object), typeof(ObjectInspectorView), new PropertyMetadata(null, (s, e) =>
            {
                if (s is ObjectInspectorView ow && ow.HostContent.DataContext is ObjectInspectorViewModel om)
                {
                    om.InspectObject = e.NewValue;
                }
            }));


        public ObjectInspectorView()
        {
            InitializeComponent();

            HostContent.DataContext = new ObjectInspectorViewModel();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
