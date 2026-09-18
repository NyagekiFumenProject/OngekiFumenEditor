using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class DockableObjectInteractiveAction : DefaultObjectInteractiveAction
    {
        public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            return editor.Fumen.Lanes.Where(x => x.IsDockableLane).FilterNull();
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            var forceMagneticToLane = editor.Setting.ForceTapHoldMagneticDockToLane;
            var forceMagnetic = editor.Setting.ForceMagneticDock;
            var enableMoveTo = !forceMagneticToLane;

            var dockable = (ILaneDockable)obj;

            if (CheckAndAdjustY(dockable, relativePoint.Y, editor) is double y && editor.ConvertYToTGrid_DesignMode(y) is TGrid tGrid)
            {
                var closestLaneObjects = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .GroupBy(x => x.Item1)
                    .OrderBy(x => x.Key)
                    .FirstOrDefault();

                var closestLaneObject = closestLaneObjects?
                    .OrderByDescending(x => x.startObject.RecordId)
                    .FirstOrDefault();

                /*
                var closestLaneObject = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Where(x => x is not IColorfulLane)
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .OrderBy(x => x.Item1)
                    .FirstOrDefault();
                */

                var magneticDockDistance = forceMagneticToLane || forceMagnetic ? int.MaxValue : 8;

                if (closestLaneObject?.startObject is not null)
                {
                    //����Ѿ����ŵ�����Ļ����ǾͿ�������϶�����һ�����Ϲ�����������������������Լ�����ģ�
                    //��ô��ǿ�Ƹ��������ˮƽλ�óɶ�Ӧ�����
                    if (closestLaneObject?.Item1 < magneticDockDistance || //�����϶�����һ������
                        closestLaneObject?.startObject == dockable.ReferenceLaneStart) //û�ϵ���һ������(������Ҫ����ˮƽλ��)
                    {
                        relativePoint.X = closestLaneObject?.Value ?? default;
                        dockable.ReferenceLaneStart = closestLaneObject?.startObject;
                        //Log.LogDebug($"auto dock to lane : {closestLaneObject.startObject}");
                        enableMoveTo = true;
                    }
                }
            }

            //���ForceTapHoldMagneticDockToLane=true,����Ҫ�����ն�λ��
            if (enableMoveTo)
                base.OnMoveCanvas(obj, relativePoint, editor);
        }

        public override double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject)
                return x;
            return base.CheckAndAdjustX(obj, x, editor);
        }

        public override double? CheckAndAdjustY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
        {
            var dockable = (ILaneDockable)obj;
            if (dockable.ReferenceLaneStart is not null)
            {
                if (editor.ConvertYToTGrid_DesignMode(y) is not TGrid tGrid)
                    return default;
                if (tGrid < dockable.ReferenceLaneStart.MinTGrid)
                    return editor.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MinTGrid);
                if (tGrid > dockable.ReferenceLaneStart.MaxTGrid)
                    return editor.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MaxTGrid);
            }

            return base.CheckAndAdjustY(obj, y, editor);
        }

        protected virtual double? CalculateConnectableObjectCurrentRelativeX(ConnectableStartObject startObject, TGrid tGrid, FumenVisualEditorViewModel editor)
        {
            if (tGrid < startObject.TGrid)
                return default;

            var xGrid = startObject.CalulateXGrid(tGrid);
            if (xGrid == null)
                return default;

            return XGridCalculator.ConvertXGridToX(xGrid, editor);
        }
    }
}
