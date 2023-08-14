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
    public class TGridTypeUIViewModel : CommonUIViewModelBase<TGrid>
    {
        private object cacheGrid = DependencyProperty.UnsetValue;
        public object Grid
        {
            get
            {
                var val = ProxyValue;
                if (val is TGrid tGrid)
                    return tGrid.Grid;
                return cacheGrid;
            }
            set
            {
                if (int.TryParse(value?.ToString(), out var v))
                {
                    cacheGrid = v;
                    TryApplyValue(v, Unit, ResT);
                    NotifyOfPropertyChange(() => Grid);
                }
            }
        }

        private object cacheUnit = DependencyProperty.UnsetValue;
        public object Unit
        {
            get
            {
                var val = ProxyValue;
                if (val is TGrid tGrid)
                    return tGrid.Unit;
                return cacheUnit;
            }
            set
            {
                if (float.TryParse(value?.ToString(), out var v))
                {
                    cacheUnit = v;
                    TryApplyValue(Grid, v, ResT);
                    NotifyOfPropertyChange(() => Unit);
                }
            }
        }

        private object cacheResT = TGrid.DEFAULT_RES_T;
        public object ResT
        {
            get
            {
                var val = ProxyValue;
                if (val is TGrid tGrid)
                    return tGrid.ResT;
                return cacheResT;
            }
            set
            {
                if (uint.TryParse(value?.ToString(), out var v))
                {
                    cacheUnit = v;
                    TryApplyValue(Grid, Unit, v);
                    NotifyOfPropertyChange(() => ResT);
                }
            }
        }

        private void TryApplyValue(object Grid, object Unit, object ResT)
        {
            if (Grid is int grid && Unit is float unit && ResT is uint resT)
            {
                var oldVal = TypedProxyValue;
                var newVal = new TGrid(unit, grid, resT);
                var refTarget = this;

                TypedProxyValue = newVal;
            }
        }

        public TGridTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {

        }
    }
}
