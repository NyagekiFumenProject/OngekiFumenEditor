using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
    public class ObjectInteractiveManager
    {
        private Dictionary<Type, ObjectInteractiveActionBase> actionProcessMap = new();
        private ObjectInteractiveActionBase defaultAction = new DefaultObjectInteractiveAction();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ObjectInteractiveActionBase GetInteractive(OngekiObjectBase ongeki)
        {
            var type = ongeki.GetType();
            return actionProcessMap.TryGetValue(type, out var action) ? action : (actionProcessMap[type] = GetInteractiveInternal(ongeki));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ObjectInteractiveActionBase GetInteractiveInternal(OngekiObjectBase ongeki)
        {
            return ongeki switch
            {
                HoldEnd { ReferenceLaneStart: { IsWallLane: false } } => new HoldEndObjectInteractiveAction(),
                HoldEnd { ReferenceLaneStart: { IsWallLane: true } } => new WallHoldObjectInteractiveAction(),
                Hold => new HoldObjectInteractiveAction(),
                ILaneDockable => new DockableObjectInteractiveAction(),
                ConnectableObjectBase or LaneCurvePathControlObject => new ConnectableObjectInteractiveAction(),
                IHorizonPositionObject => new HorizonObjectInteractiveAction(),
                _ => defaultAction,
            };
        }
    }
}
