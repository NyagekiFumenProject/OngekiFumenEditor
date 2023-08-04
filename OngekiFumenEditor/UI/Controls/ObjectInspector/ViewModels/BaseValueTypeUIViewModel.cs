using Caliburn.Micro;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class BaseValueTypeUIViewModel : CommonUIViewModelBase
    {
        public BaseValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }
    }
}
