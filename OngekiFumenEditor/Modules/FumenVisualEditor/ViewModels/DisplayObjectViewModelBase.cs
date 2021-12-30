using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Converters;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public abstract class DisplayObjectViewModelBase : PropertyChangedBase, IEditorDisplayableViewModel,IViewAware
    {
        private bool isSelected;
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }
            set
            {
                isSelected = value;
                NotifyOfPropertyChange(() => IsSelected);

                EditorViewModel?.OnSelectPropertyChanged(this, value);
            }
        }

        protected OngekiObjectBase referenceOngekiObject;
        public virtual OngekiObjectBase ReferenceOngekiObject
        {
            get { return referenceOngekiObject; }
            set
            {
                referenceOngekiObject = value;
                NotifyOfPropertyChange(() => ReferenceOngekiObject);
                NotifyOfPropertyChange(() => CanMoveX);
                NotifyOfPropertyChange(() => IsTimelineObject);
            }
        }

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
            }
        }

        public void OnDragEnd(Point pos)
        {
            if (View is null)
                return;

            var movePoint = new Point(
                dragViewStartPoint.X + (pos.X - dragStartPoint.X),
                dragViewStartPoint.Y + (pos.Y - dragStartPoint.Y)
                );

            //这里限制一下
            movePoint.X = Math.Max(0, Math.Min(EditorViewModel.CanvasWidth, movePoint.X));
            movePoint.Y = Math.Max(0, Math.Min(EditorViewModel.CanvasHeight, movePoint.Y));

            MoveCanvas(movePoint);

            //Log.LogInfo($"movePoint: {movePoint}");
        }

        Point dragViewStartPoint = default;
        Point dragStartPoint = default;

        public event EventHandler<ViewAttachedEventArgs> ViewAttached;

        public void OnDragStart(Point pos)
        {
            if (View is null)
                return;

            var x = (double)View.GetValue(Canvas.LeftProperty);
            var y = (double)View.GetValue(Canvas.TopProperty);
            dragViewStartPoint = new Point(x, y);
            dragStartPoint = pos;

            //Log.LogInfo($"dragViewStartPoint: {dragViewStartPoint}, dragStartPoint: {dragStartPoint}");
        }

        public void OnMouseClick(Point pos)
        {
            IsSelected = !IsSelected;
        }

        public virtual void MoveCanvas(Point relativePoint)
        {
            if (EditorViewModel is FumenVisualEditorViewModel hostModelView)
            {
                if (ReferenceOngekiObject is ITimelineObject timeObj)
                {
                    var ry = CheckAndAdjustY(relativePoint.Y);
                    if (TGridCalculator.ConvertYToTGrid(ry, hostModelView) is TGrid tGrid)
                    {
                        timeObj.TGrid = tGrid;
                        //Log.LogInfo($"Y: {ry} , TGrid: {timeObj.TGrid}");
                    }
                }

                if (ReferenceOngekiObject is IHorizonPositionObject posObj)
                {
                    var x = CheckAndAdjustX(relativePoint.X);
                    var xGrid = XGridCalculator.ConvertXToXGrid(x, hostModelView);
                    posObj.XGrid = xGrid;
                    //Log.LogInfo($"x : {x:F4} , posObj.XGrid.Unit : {posObj.XGrid.Unit} , xConvertBack : {XGridCalculator.ConvertXGridToX(posObj.XGrid, hostModelView)}");
                }
            }
            else
            {
                Log.LogInfo("Can't move object in canvas because it's not ready.");
            }
        }

        public virtual double CheckAndAdjustY(double y)
        {
            var s = y;
            y = EditorViewModel.CanvasHeight - y;
            var enableMagneticAdjust = !(editorViewModel?.Setting.IsPreventTimelineAutoClose ?? false);
            var mid = enableMagneticAdjust ? editorViewModel?.TGridUnitLineLocations?.Select(z => new
            {
                distance = Math.Abs(z.Y - s),
                y = z.Y
            })?.Where(z => z.distance < 4)?.OrderBy(x => x.distance)?.ToList() : default;
            var nearestUnitLine = mid?.FirstOrDefault();
            var fin = nearestUnitLine != null ? (EditorViewModel.CanvasHeight - nearestUnitLine.y) : y;
            //Log.LogInfo($"before y={y:F2} ,select:({nearestUnitLine?.y:F2}) ,fin:{fin:F2}");
            return fin;
        }

        public virtual double CheckAndAdjustX(double x)
        {
            //todo 基于二分法查询最近
            var enableMagneticAdjust = !(editorViewModel?.Setting.IsPreventXAutoClose ?? false);
            var mid = enableMagneticAdjust ? editorViewModel?.XGridUnitLineLocations?.Select(z => new
            {
                distance = Math.Abs(z.X - x),
                x = z.X
            })?.Where(z => z.distance < 4)?.OrderBy(x => x.distance)?.ToList() : default;
            var nearestUnitLine = mid?.FirstOrDefault();
            //Log.LogInfo($"nearestUnitLine in:{x:F2} distance:{nearestUnitLine?.distance:F2} x:{nearestUnitLine?.x:F2}");
            return nearestUnitLine != null ? nearestUnitLine.x : x;
        }

        public static BindingBase XGridMapToXBinding { get; }
        public static BindingBase TGridMapToYBinding { get; }

        static DisplayObjectViewModelBase()
        {
            var xb = new MultiBinding()
            {
                Converter = new XGridCanvasConverter(),
            };
            xb.Bindings.Add(new Binding("ReferenceOngekiObject.XGrid.Unit"));
            xb.Bindings.Add(new Binding("EditorViewModel"));
            xb.Bindings.Add(new Binding("EditorViewModel.Setting"));

            XGridMapToXBinding = xb;

            var tb = new MultiBinding()
            {
                Converter = new TGridCanvasConverter(),
            };
            tb.Bindings.Add(new Binding("ReferenceOngekiObject.TGrid.Grid"));
            tb.Bindings.Add(new Binding("ReferenceOngekiObject.TGrid.Unit"));
            tb.Bindings.Add(new Binding("ReferenceOngekiObject.TGrid"));
            tb.Bindings.Add(new Binding("EditorViewModel"));

            TGridMapToYBinding = tb;
        }

        protected virtual void OnAttachedView(object view)
        {
            var element = view as FrameworkElement;

            if (ReferenceOngekiObject is IHorizonPositionObject)
            {
                element.SetBinding(Canvas.LeftProperty, XGridMapToXBinding);
            }

            if (ReferenceOngekiObject is ITimelineObject)
            {
                element.SetBinding(Canvas.TopProperty, TGridMapToYBinding);
            }

            Refresh();
        }

        public void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is OngekiObjectBase obj)
                ReferenceOngekiObject = obj;
            EditorViewModel = editorViewModel;
        }

        public FrameworkElement View { get; private set; }

        public void AttachView(object view, object context = null)
        {
            View = view as FrameworkElement;
            OnAttachedView(View);
        }

        public object GetView(object context = null) => View;
    }

    public abstract class DisplayObjectViewModelBase<T> : DisplayObjectViewModelBase where T : OngekiObjectBase, new()
    {
        public override OngekiObjectBase ReferenceOngekiObject
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

    [MapToView(ViewType = typeof(DisplayTextLineObjectViewBase))]
    public abstract class DisplayTextLineObjectViewModelBase<T> : DisplayObjectViewModelBase<T> where T : OngekiObjectBase, new()
    {
        public new T ReferenceOngekiObject
        {
            get
            {
                return base.ReferenceOngekiObject as T;
            }
            set
            {
                base.ReferenceOngekiObject = value;
            }
        }

        public abstract Brush DisplayBrush { get; }
        public virtual string DisplayName => ReferenceOngekiObject.IDShortName;
        public abstract BindingBase DisplayValueBinding { get; }

        protected override void OnAttachedView(object v)
        {
            base.OnAttachedView(v);

            if (v is DisplayTextLineObjectViewBase view && DisplayValueBinding is not null)
                view.displayValueTextBlock.SetBinding(TextBlock.TextProperty, DisplayValueBinding);
        }
    }
}
