using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
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
        private object cacheGrid = DependencyProperty.UnsetValue;
        public object Grid
        {
            get
            {
                var val = ProxyValue;
                if (val is XGrid xGrid)
                    return xGrid.Grid;
                return cacheGrid;
            }
            set
            {
                if (int.TryParse(value?.ToString(), out var v))
                {
                    cacheGrid = v;
                    TryApplyValue(v, Unit, ResX);
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
                if (val is XGrid xGrid)
                    return xGrid.Unit;
                return cacheUnit;
            }
            set
            {
                if (float.TryParse(value?.ToString(), out var v))
                {
                    cacheUnit = v;
                    TryApplyValue(Grid, v, ResX);
                    NotifyOfPropertyChange(() => Unit);
                }
            }
        }

        private object cacheResX = XGrid.DEFAULT_RES_X;
        public object ResX
        {
            get
            {
                var val = ProxyValue;
                if (val is XGrid xGrid)
                    return xGrid.ResX;
                return cacheResX;
            }
            set
            {
                if (uint.TryParse(value?.ToString(), out var v))
                {
                    cacheUnit = v;
                    TryApplyValue(Grid, Unit, v);
                    NotifyOfPropertyChange(() => ResX);
                }
            }
        }

        private void TryApplyValue(object Grid, object Unit, object ResX)
        {
            if (Grid is int grid && Unit is float unit && ResX is uint resX)
            {
                var oldVal = TypedProxyValue;
                var newVal = new XGrid(unit, grid, resX);
                var refTarget = this;

                TypedProxyValue = newVal;
            }
        }

        public XGridTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {

        }

        public void SetNull()
        {
            var rollback = TypedProxyValue;
            try
            {
                TypedProxyValue = null;
            }
            catch (Exception e)
            {
                Log.LogError($"Can't set null for prop {PropertyInfo.DisplayPropertyName}: {e.Message}");
                TypedProxyValue = rollback;
            }
        }
    }
}
