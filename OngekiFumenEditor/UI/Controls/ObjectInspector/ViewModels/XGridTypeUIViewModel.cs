using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class XGridTypeUIViewModel : CommonUIViewModelBase<XGrid>
    {
        public int Grid
        {
            get => TypedProxyValue.Grid;
            set
            {
                if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
                    undoable.ExecuteSubPropertySetAction(nameof(XGrid.Grid), (val) => TypedProxyValue.Grid = val, Grid, value);
                else
                    TypedProxyValue.Grid = value;

                NotifyOfPropertyChange(() => Grid);
            }
        }

        public float Unit
        {
            get => TypedProxyValue.Unit;
            set
            {
                if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
                    undoable.ExecuteSubPropertySetAction(nameof(XGrid.Unit), (val) => TypedProxyValue.Unit = val, Unit, value);
                else
                    TypedProxyValue.Unit = value;

                NotifyOfPropertyChange(() => Unit);
            }
        }

        public uint ResX
        {
            get => TypedProxyValue.ResX;
            set
            {
                if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
                    undoable.ExecuteSubPropertySetAction(nameof(XGrid.ResX), (val) => TypedProxyValue.ResX = val, ResX, value);
                else
                    TypedProxyValue.ResX = value;

                NotifyOfPropertyChange(() => ResX);
            }
        }

        public XGridTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {
            if (PropertyInfo.OwnerObject is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged += NotifyProperty_PropertyChanged;
            }
        }

        private void NotifyProperty_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(XGrid))
            {
                Refresh();
            }
        }
    }
}
