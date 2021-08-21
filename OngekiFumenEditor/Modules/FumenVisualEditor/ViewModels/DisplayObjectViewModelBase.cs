using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Converters;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public abstract class DisplayObjectViewModelBase : PropertyChangedBase, IViewAware
    {
        protected IOngekiObject referenceOngekiObject;

        public virtual IOngekiObject ReferenceOngekiObject
        {
            get { return referenceOngekiObject; }
            set
            {
                referenceOngekiObject = value;
                NotifyOfPropertyChange(() => ReferenceOngekiObject);
            }
        }

        private double y;

        public event EventHandler<ViewAttachedEventArgs> ViewAttached;

        public double Y
        {
            get { return y; }
            set
            {
                y = value;
                NotifyOfPropertyChange(() => Y);
            }
        }

        public object View { get; private set; }

        public object Context { get; private set; }

        /// <summary>
        /// 表示此物件是否能设置水平位置(即此物件是否支持XGrid)
        /// </summary>
        public bool CanMoveX => ReferenceOngekiObject is IHorizonPositionObject;

        /// <summary>
        /// 表示此物件是否能设置时间轴位置(即此物件是否支持TGrid)
        /// </summary>
        public bool IsTimelineObject => ReferenceOngekiObject is ITimelineObject;

        private FumenVisualEditorViewModel editorViewModel;
        public FumenVisualEditorViewModel EditorViewModel
        {
            get
            {
                return editorViewModel;
            }
            set
            {
                editorViewModel = value;
                NotifyOfPropertyChange(() => EditorViewModel);
                //OnEditorViewModelChanged();
            }
        }

        public virtual void MoveCanvas(Point relativePoint)
        {
            Y = relativePoint.Y;

            if (ReferenceOngekiObject is IHorizonPositionObject posObj && EditorViewModel is FumenVisualEditorViewModel hostModelView)
            {
                var x = CheckAndAdjustX(relativePoint.X);
                var xgridValue = (x - hostModelView.CanvasWidth / 2) / (hostModelView.XUnitSize / hostModelView.UnitCloseSize);
                var near = xgridValue > 0 ? Math.Floor(xgridValue + 0.5) : Math.Ceiling(xgridValue - 0.5);
                posObj.XGrid.Unit = Math.Abs(xgridValue - near) < 0.00001 ? (int)near : (float)xgridValue;
                //Log.LogInfo($"xgridValue : {xgridValue:F4} , posObj.XGrid.Unit : {posObj.XGrid.Unit}");
            }
            else
            {
                Log.LogInfo("Can't move object in canvas because it's not ready.");
            }
        }

        public double CheckAndAdjustX(double x)
        {
            //todo 基于二分法查询最近
            var editorViewModel = EditorViewModel;
            var enableMagneticAdjust = !(editorViewModel?.IsPreventXAutoClose ?? false);
            var mid = enableMagneticAdjust ? editorViewModel?.XGridUnitLineLocations?.Select(z => new
            {
                distance = Math.Abs(z.X - x),
                x = z.X
            })?.Where(z => z.distance < 10)?.OrderBy(x => x.distance)?.ToList() : default;
            var nearestUnitLine = mid?.FirstOrDefault();
            //Log.LogInfo($"nearestUnitLine in:{x:F2} distance:{nearestUnitLine?.distance:F2} x:{nearestUnitLine?.x:F2}");
            return nearestUnitLine != null ? nearestUnitLine.x : x;
        }

        protected virtual void OnAttachedView(object view)
        {
            var element = view as FrameworkElement;

            if (ReferenceOngekiObject is IHorizonPositionObject)
            {
                var xBinding = new MultiBinding()
                {
                    Converter = new XGridCanvasConverter(),
                };
                xBinding.Bindings.Add(new Binding("ReferenceOngekiObject.XGrid.Unit"));
                xBinding.Bindings.Add(new Binding("EditorViewModel"));
                element.SetBinding(Canvas.LeftProperty, xBinding);
            }
            
            element.SetBinding(Canvas.TopProperty, "Y");

            Refresh();
        }

        public void AttachView(object view, object context = null)
        {
            View = view;
            Context = context;

            OnAttachedView(View);
        }

        public object GetView(object context = null) => View;
    }

    public abstract class DisplayObjectViewModelBase<T> : DisplayObjectViewModelBase where T : IOngekiObject, new()
    {
        public override IOngekiObject ReferenceOngekiObject
        {
            get
            {
                if (referenceOngekiObject is null)
                {
                    ReferenceOngekiObject = new T();
                }
                return base.ReferenceOngekiObject;
            }
            set
            {
                base.ReferenceOngekiObject = value;
            }
        }
    }
}
