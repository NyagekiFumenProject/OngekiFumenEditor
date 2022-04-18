using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Converters;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public virtual int RenderOrderZ => 5;
        public bool NeedCanvasPointsBinding => true;

        public virtual bool IsSelected
        {
            get
            {
                return (ReferenceOngekiObject as ISelectableObject)?.IsSelected ?? false;
            }
            set
            {
                if (ReferenceOngekiObject is ISelectableObject selectable)
                    selectable.IsSelected = value;
                NotifyOfPropertyChange(() => IsSelected);
            }
        }

        private double canvasX = 0;
        public double CanvasX
        {
            get => canvasX;
            set => Set(ref canvasX, value);
        }

        private double canvasY = 0;
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
                this.RegisterOrUnregisterPropertyChangeEvent(referenceOngekiObject, value, OnOngekiObjectPropChanged);
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
            var oldPos = dragStartCanvasPoint;
            var newPos = new Point(CanvasX, CanvasY);
            EditorViewModel?.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("物件拖动",
                () =>
                {
                    MoveCanvas(newPos);
                }, () =>
                {
                    MoveCanvas(oldPos);
                }));
        }

        public virtual void OnDragMoving(Point pos)
        {
            var movePoint = new Point(
                dragStartCanvasPoint.X + (pos.X - dragStartPoint.X),
                dragStartCanvasPoint.Y + (pos.Y - dragStartPoint.Y)
                );

            //这里限制一下
            movePoint.X = Math.Max(0, Math.Min(EditorViewModel.TotalDurationHeight, movePoint.X));
            movePoint.Y = Math.Max(0, Math.Min(EditorViewModel.TotalDurationHeight, movePoint.Y));

            //Log.LogDebug($"movePoint: ({pos.X:F2},{pos.Y:F2}) -> ({movePoint.X:F2},{movePoint.Y:F2})");

            MoveCanvas(movePoint);
        }

        Point dragStartCanvasPoint = default;
        Point dragStartPoint = default;

        public virtual void OnDragStart(Point pos)
        {
            var x = CanvasX;
            var y = CanvasY;
            if (double.IsNaN(x))
                x = default;
            if (double.IsNaN(y))
                y = default;

            dragStartCanvasPoint = new Point(x, y);
            dragStartPoint = pos;
            //Log.LogInfo($"OnDragStart");
        }

        public virtual void OnMouseClick(Point pos) => EditorViewModel?.NotifyObjectClicked(this);

        public virtual void MoveCanvas(Point relativePoint)
        {
            if (EditorViewModel is FumenVisualEditorViewModel hostModelView)
            {
                if (ReferenceOngekiObject is ITimelineObject timeObj)
                {
                    var ry = CheckAndAdjustY(relativePoint.Y);
                    if (ry is double dry && TGridCalculator.ConvertYToTGrid(dry, hostModelView) is TGrid tGrid)
                    {
                        timeObj.TGrid = tGrid;
                        //Log.LogInfo($"Y: {ry} , TGrid: {timeObj.TGrid}");
                    }

                    RecaulateCanvasY();
                }

                if (ReferenceOngekiObject is IHorizonPositionObject posObj)
                {
                    var rx = CheckAndAdjustX(relativePoint.X);
                    if (rx is double drx)
                    {
                        var xGrid = XGridCalculator.ConvertXToXGrid(drx, hostModelView);
                        posObj.XGrid = xGrid;
                    }

                    //Log.LogInfo($"x : {x:F4} , posObj.XGrid.Unit : {posObj.XGrid.Unit} , xConvertBack : {XGridCalculator.ConvertXGridToX(posObj.XGrid, hostModelView)}");
                    RecaulateCanvasX();
                }
            }
            else
            {
                Log.LogInfo("Can't move object in canvas because it's not ready.");
            }
        }

        private class TempCloseLine
        {
            public double distance { get; set; }
            public double value { get; set; }
        }

        public virtual double? CheckAndAdjustY(double y)
        {
            var enableMagneticAdjust = !(editorViewModel?.Setting.DisableTGridMagneticDock ?? false);
            if (!enableMagneticAdjust)
                return y;

            var forceMagneticAdjust = editorViewModel?.Setting.ForceMagneticDock ?? false;
            var fin = forceMagneticAdjust ? TGridCalculator.TryPickClosestBeatTime((float)y, EditorViewModel, 240) : TGridCalculator.TryPickMagneticBeatTime((float)y, 4, EditorViewModel, 240);
            var ry = fin.y;
            if (fin.tGrid == null)
                ry = y;
            //Log.LogDebug($"before y={y:F2} ,select:({fin.tGrid}) ,fin:{ry:F2}");
            return ry;
        }

        public virtual double? CheckAndAdjustX(double x)
        {
            //todo 基于二分法查询最近
            var enableMagneticAdjust = !(editorViewModel?.Setting.DisableXGridMagneticDock ?? false);
            var forceMagneticAdjust = editorViewModel?.Setting.ForceMagneticDock ?? false;
            var dockableTriggerDistance = forceMagneticAdjust ? int.MaxValue : 4;
            using var d1 = ObjectPool<List<TempCloseLine>>.GetWithUsingDisposable(out var mid, out var _);
            mid.Clear();
            mid.AddRange(enableMagneticAdjust ? editorViewModel?.XGridUnitLineLocations?.Select(z =>
            {
                var r = ObjectPool<TempCloseLine>.Get();
                r.distance = Math.Abs(z.X - x);
                r.value = z.X;
                return r;
            })?.Where(z => z.distance < dockableTriggerDistance)?.OrderBy(x => x.distance)?.ToList() : Enumerable.Empty<TempCloseLine>());
            var nearestUnitLine = mid?.FirstOrDefault();
            double? fin = nearestUnitLine != null ? nearestUnitLine.value : (forceMagneticAdjust ? null : x);
            //Log.LogInfo($"nearestUnitLine x:{x:F2} distance:{nearestUnitLine?.distance:F2} fin:{fin}");
            mid.ForEach(x => ObjectPool<TempCloseLine>.Return(x));
            mid.Clear();
            return fin;
        }

        public static BindingBase XGridMapToXBinding { get; }
        public static BindingBase TGridMapToYBinding { get; }

        public IDisplayableObject DisplayableObject => ReferenceOngekiObject as IDisplayableObject;

        public void RecaulateCanvasX()
        {
            if (isHorizonPositionObject == false || EditorViewModel is null)
                return;

            var xGrid = ((IHorizonPositionObject)ReferenceOngekiObject).XGrid;
            var modelView = EditorViewModel;
            var xgridUnit = xGrid.Unit + xGrid.Grid / xGrid.ResX;
            var x = xgridUnit * (XGridCalculator.CalculateXUnitSize(modelView) / modelView.Setting.XGridUnitSpace) + modelView.CanvasWidth / 2;

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

        protected virtual void OnOngekiObjectPropChanged(object sender, PropertyChangedEventArgs arg)
        {
            switch (arg.PropertyName)
            {
                case nameof(TGrid):
                    RecaulateCanvasY();
                    break;

                case nameof(XGrid):
                    RecaulateCanvasX();
                    break;
                case nameof(GridBase.Unit):
                case nameof(GridBase.Grid):
                    RecaulateCanvasXY();
                    break;
                case nameof(ISelectableObject.IsSelected):
                    NotifyOfPropertyChange(() => IsSelected);
                    break;
                default:
                    break;
            }
        }

        public void OnEditorRedrawObjects()
        {
            RecaulateCanvasXY();
        }

        public virtual DisplayObjectViewModelBase Copy()
        {
            var obj = ReferenceOngekiObject;
            if (obj is not IDisplayableObject displayable
                //暂不支持 以下类型的复制粘贴
                //|| obj is ConnectableObjectBase
                )
                return default;

            var copyNewViewModel = CacheLambdaActivator.CreateInstance(displayable.ModelViewType) as DisplayObjectViewModelBase;
            var newObj = copyNewViewModel.ReferenceOngekiObject;
            newObj.Copy(obj, EditorViewModel.Fumen);
            return copyNewViewModel;
        }
    }

    public abstract class DisplayObjectViewModelBase<T> : DisplayObjectViewModelBase where T : OngekiObjectBase, IDisplayableObject, new()
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
    public abstract class DisplayTextLineObjectViewModelBase<T> : DisplayObjectViewModelBase<T> where T : OngekiObjectBase, IDisplayableObject, new()
    {
        public string DisplayName => ReferenceOngekiObject.IDShortName;
        public abstract Brush DisplayBrush { get; }
    }
}
