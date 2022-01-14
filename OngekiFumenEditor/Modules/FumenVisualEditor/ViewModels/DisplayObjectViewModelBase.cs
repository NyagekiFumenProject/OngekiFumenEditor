using Caliburn.Micro;
using Gemini.Framework;
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
    public abstract class DisplayObjectViewModelBase : PropertyChangedBase, IEditorDisplayableViewModel
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

        public double canvasX = 0;
        public double CanvasX
        {
            get => canvasX;
            set => Set(ref canvasX, value);
        }

        public double canvasY = 0;
        public double CanvasY
        {
            get => canvasY;
            set => Set(ref canvasY, value);
        }

        private bool isHorizonPositionObject = false;
        private bool isTimelineObject = false;

        protected OngekiObjectBase referenceOngekiObject;
        public virtual OngekiObjectBase ReferenceOngekiObject
        {
            get { return referenceOngekiObject; }
            set
            {
                referenceOngekiObject = value;
                isHorizonPositionObject = value is IHorizonPositionObject;
                isTimelineObject = value is ITimelineObject;
                NotifyOfPropertyChange(() => ReferenceOngekiObject);
            }
        }

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

        public virtual void OnDragEnd(Point pos)
        {
            OnDragMoving(pos);
            //Log.LogInfo($"OnDragEnd");
        }

        public virtual void OnDragMoving(Point pos)
        {
            var movePoint = new Point(
                dragViewStartPoint.X + (pos.X - dragStartPoint.X),
                dragViewStartPoint.Y - (pos.Y - dragStartPoint.Y)
                );

            //这里限制一下
            movePoint.X = Math.Max(0, Math.Min(EditorViewModel.CanvasWidth, movePoint.X));
            movePoint.Y = Math.Max(0, Math.Min(EditorViewModel.CanvasHeight, movePoint.Y));

            MoveCanvas(movePoint);

            //Log.LogInfo($"OnDragMoving");
            //Log.LogInfo($"movePoint: {movePoint}");
        }

        Point dragViewStartPoint = default;
        Point dragStartPoint = default;

        public virtual void OnDragStart(Point pos)
        {
            var x = CanvasX;
            var y = CanvasY;
            if (double.IsNaN(x))
                x = default;
            if (double.IsNaN(y))
                y = default;
            dragViewStartPoint = new Point(x, y);
            dragStartPoint = pos;

            //Log.LogInfo($"OnDragStart");
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
                    RecaulateCanvasY();
                }

                if (ReferenceOngekiObject is IHorizonPositionObject posObj)
                {
                    var x = CheckAndAdjustX(relativePoint.X);
                    var xGrid = XGridCalculator.ConvertXToXGrid(x, hostModelView);
                    posObj.XGrid = xGrid;
                    //Log.LogInfo($"x : {x:F4} , posObj.XGrid.Unit : {posObj.XGrid.Unit} , xConvertBack : {XGridCalculator.ConvertXGridToX(posObj.XGrid, hostModelView)}");
                    RecaulateCanvasX();
                }
            }
            else
            {
                Log.LogInfo("Can't move object in canvas because it's not ready.");
            }
        }

        public virtual double CheckAndAdjustY(double y)
        {
            return y;
            /*
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
            Log.LogInfo($"before y={y:F2} ,select:({nearestUnitLine?.y:F2}) ,fin:{fin:F2}");
            return fin;
            */
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

        public void RecaulateCanvasX()
        {
            if (isHorizonPositionObject == false || EditorViewModel is null)
                return;

            var xGrid = ((IHorizonPositionObject)ReferenceOngekiObject).XGrid;
            var modelView = EditorViewModel;
            var xgridUnit = xGrid.Unit + xGrid.Grid / xGrid.ResX;
            var x = xgridUnit * (modelView.XUnitSize / modelView.Setting.UnitCloseSize) + modelView.CanvasWidth / 2;

            CanvasX = x;
        }

        public void RecaulateCanvasY()
        {
            if (isTimelineObject == false || EditorViewModel is null)
                return;
            var tGrid = ((ITimelineObject)ReferenceOngekiObject).TGrid;
            var y = TGridCalculator.ConvertTGridToY(tGrid, EditorViewModel);
            CanvasY = y;
            //Log.LogInfo($"Y: {CanvasY} , TGrid: {tGrid}");
        }

        public void RecaulateCanvasXY()
        {
            RecaulateCanvasX();
            RecaulateCanvasY();
        }

        public virtual void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is OngekiObjectBase obj)
                ReferenceOngekiObject = obj;
            EditorViewModel = editorViewModel;
        }
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
        public string DisplayName => ReferenceOngekiObject.IDShortName;
        public abstract Brush DisplayBrush { get; }
    }
}
