using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
	public class ObjectInteractiveManager
	{
		private Dictionary<Type, ObjectInteractiveActionBase> actionProcessMap = new();

		private ObjectInteractiveActionBase defaultAction = new DefaultObjectInteractiveAction();
		private ObjectInteractiveActionBase holdEndAction = new HoldEndObjectInteractiveAction();
		private ObjectInteractiveActionBase wallHoldEndAction = new WallHoldObjectInteractiveAction();

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
				IHorizonPositionObject => new HorizonObjectInteractiveAction(),
				_ => defaultAction,
			};
		}
	}
}
