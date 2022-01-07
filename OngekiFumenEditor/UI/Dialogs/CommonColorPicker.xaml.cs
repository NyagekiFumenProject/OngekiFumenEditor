using Caliburn.Micro;
using ColorPicker.Models;
using MahApps.Metro.Controls;
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
using System.Windows.Shapes;

namespace OngekiFumenEditor.UI.Dialogs
{
    /// <summary>
    /// CommonColorPicker.xaml 的交互逻辑
    /// </summary>
    public partial class CommonColorPicker : MetroWindow, INotifyPropertyChanged
    {
        private readonly Func<Color> getter;
        private readonly Action<Color> setter;

        public event PropertyChangedEventHandler PropertyChanged;

        public Color CurrentColor
        {
            get => getter();
            set
            {
                setter(value);
                PropertyChanged?.Invoke(this, new(nameof(CurrentColor)));
            }
        }

        public CommonColorPicker(Func<Color> getter, Action<Color> setter, string title)
        {
            this.getter = getter;
            this.setter = setter;
            InitializeComponent();
            this.DataContext = this;
            Title = title;
        }
    }
}
