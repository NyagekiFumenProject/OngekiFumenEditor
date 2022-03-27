using System;
using System.Collections.Generic;
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

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UI.Controls
{
    /// <summary>
    /// CommonOperationButton.xaml 的交互逻辑
    /// </summary>
    public partial class CommonOperationButton : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(CommonOperationButton), new PropertyMetadata(""));

        public Brush DecoratorBrush
        {
            get { return (Brush)GetValue(DecoratorBrushProperty); }
            set { SetValue(DecoratorBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DecoratorBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DecoratorBrushProperty =
            DependencyProperty.Register("DecoratorBrush", typeof(Brush), typeof(CommonOperationButton), new PropertyMetadata(Brushes.Black));

        public CommonOperationButton()
        {
            InitializeComponent();
            Board.DataContext = this;
        }
    }
}
