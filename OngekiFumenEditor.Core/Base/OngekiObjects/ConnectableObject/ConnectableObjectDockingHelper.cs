using OngekiFumenEditor.Core.Utils;
using System.Linq;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject
{
    public static class ConnectableObjectDockingHelper
    {
        public static void RelocateDockableObjects(OngekiFumen fumen, ConnectableObjectBase affectedObj)
        {
            var start = affectedObj?.ReferenceStartObject;

            RelocateDockableObjects(fumen, affectedObj, start);
            if (affectedObj is ConnectableChildObjectBase child)
                RelocateDockableObjects(fumen, child.PrevObject, start);
        }

        public static void RelocateDockableAllObjects(OngekiFumen fumen, ConnectableStartObject start)
        {
            foreach (var child in start.Children)
                RelocateDockableObjects(fumen, child.PrevObject, start);
        }

        public static void RelocateDockableObjects(OngekiFumen fumen, ConnectableObjectBase obj, ConnectableStartObject start)
        {
            if (start is null ||
                obj is null ||
                obj.NextObject is null)
                return;

            var refLaneId = obj.RecordId;
            var minTGrid = obj.TGrid;
            var maxTGrid = obj.NextObject.TGrid;

            var dockables = fumen.GetAllDisplayableObjects(minTGrid, maxTGrid)
                .OfType<ILaneDockable>()
                .Where(x => x.ReferenceLaneStrId == refLaneId)
                .Where(x => !((ISelectableObject)x).IsSelected)
                .ToHashSet();

            foreach (var dockable in dockables)
            {
                if (start.CalulateXGrid(dockable.TGrid) is XGrid xGrid)
                    dockable.XGrid = xGrid;

                if (dockable is Hold hold && hold.HoldEnd is HoldEnd end)
                {
                    if (end.RefHold?.ReferenceLaneStart?.CalulateXGrid(end.TGrid) is XGrid xGrid2)
                        end.XGrid = xGrid2;
                }
            }
        }
    }
}

