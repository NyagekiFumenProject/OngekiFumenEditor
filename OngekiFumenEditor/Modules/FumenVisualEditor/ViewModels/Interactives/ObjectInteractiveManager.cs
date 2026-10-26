using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using System;
using System.Collections.Generic;
using System.Linq;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
    public partial class ObjectInteractiveManager
    {
        private Dictionary<Type, ObjectInteractiveActionBase> actionProcessMap = new();

        private ObjectInteractiveActionBase defaultAction = new DefaultObjectInteractiveAction();
        private ObjectInteractiveActionBase holdEndAction = new HoldEndObjectInteractiveAction();
        private ObjectInteractiveActionBase wallHoldEndAction = new WallHoldObjectInteractiveAction();

        internal LaneSwitchResolver CreateLaneSwitchResolver(FumenVisualEditorViewModel editor) =>
            new(time => editor.Fumen.Lanes.GetVisibleStartObjects(time, time).OfType<LaneStartBase>(),
                x => XGridCalculator.ConvertXGridToX(x, editor));

        internal bool TryAdjustGrid(OngekiObjectBase obj, ref TGrid time, ref XGrid x, FumenVisualEditorViewModel editor, bool preserveTime = false)
            => GetInteractive(obj).TryAdjustGrid(obj, ref time, ref x, editor, preserveTime);

        internal bool AdjustPastedObjects(IEnumerable<OngekiObjectBase> roots, FumenVisualEditorViewModel editor)
        {
            var nodes = roots.SelectMany(GridClipboardGeometry.Enumerate).Distinct().ToArray();
            var values = new List<(OngekiObjectBase node, TGrid t, XGrid x)>();
            foreach (var node in nodes)
            {
                var time = (node as ITimelineObject)?.TGrid?.CopyNew();
                var x = (node as IHorizonPositionObject)?.XGrid?.CopyNew();
                if (!GetInteractive(node).TryAdjustGrid(node, ref time, ref x, editor))
                    return false;
                values.Add((node, time, x));
            }
            foreach (var value in values)
                if (value.t != null)
                    ((ITimelineObject)value.node).TGrid = value.t;
            foreach (var value in values)
                if (value.x != null)
                    ((IHorizonPositionObject)value.node).XGrid = value.x;
            return true;
        }

        public ObjectInteractiveActionBase GetInteractive(OngekiObjectBase ongeki)
        {
            var type = ongeki.GetType();

            return ongeki switch
            {
                HoldEnd { RefHold: { IsWallHold: false } } => holdEndAction,
                HoldEnd { RefHold: { IsWallHold: true } } => wallHoldEndAction,
                _ => actionProcessMap.TryGetValue(type, out var action) ? action : (actionProcessMap[type] = GetInteractiveInternal(ongeki))
            };
        }

        private ObjectInteractiveActionBase GetInteractiveInternal(OngekiObjectBase ongeki)
        {
            return ongeki switch
            {
                // HoldEnd { ReferenceLaneStart: { IsWallLane: false } } => new HoldEndObjectInteractiveAction(),
                // HoldEnd { ReferenceLaneStart: { IsWallLane: true } } => new WallHoldObjectInteractiveAction(),
                Hold => new HoldObjectInteractiveAction(),
                ILaneDockable => new DockableObjectInteractiveAction(),
                ConnectableObjectBase or LaneCurvePathControlObject => new ConnectableObjectInteractiveAction(),
                IndividualSoflanArea or IndividualSoflanArea.IndividualSoflanAreaEndIndicator => new IndividualSoflanAreaInteractiveAction(),
                IHorizonPositionObject => new HorizonObjectInteractiveAction(),
                _ => defaultAction,
            };
        }
    }
}
