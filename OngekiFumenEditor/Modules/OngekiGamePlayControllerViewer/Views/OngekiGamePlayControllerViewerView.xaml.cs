using Microsoft.CodeAnalysis;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Views
{
    /// <summary>
    /// EditorScriptDocumentView.xaml 的交互逻辑
    /// </summary>
    public partial class OngekiGamePlayControllerViewerView : UserControl
    {
        public OngekiGamePlayControllerViewerView()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RoutedEventArgs e)
        {
            ProcessUtils.OpenUrl("https://github.com/MikiraSora/AkariMindController/wiki/OngekiFumenEditor%E6%8F%92%E4%BB%B6%E4%BD%BF%E7%94%A8");
        }
    }
}
