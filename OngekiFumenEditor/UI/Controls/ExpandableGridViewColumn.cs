//WPF is shit
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.Toolkit.Core.Utilities;

namespace OngekiFumenEditor.UI.Controls
{
    public class ExpandableGridViewColumn : GridViewColumn
    {
        public ExpandableGridViewColumn()
        {

        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
        }
    }
}
