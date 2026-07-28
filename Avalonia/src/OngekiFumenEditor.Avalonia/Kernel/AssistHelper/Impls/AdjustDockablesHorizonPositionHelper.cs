using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Impls;

public static class AdjustDockablesHorizonPositionHelper
{
    private static XGrid CalculateConnectableObjectXGrid(ConnectableStartObject startObject, TGrid tGrid)
    {
        if (tGrid < startObject.TGrid)
            return default;

        return startObject.CalulateXGrid(tGrid);
    }

    public static void Execute(OngekiFumen fumen)
    {
        void execute<T>(IEnumerable<T> objs) where T : IHorizonPositionObject, ITimelineObject, ILaneDockable
        {
            foreach (var o in objs)
            {
                if (CalculateConnectableObjectXGrid(o.ReferenceLaneStart, o.TGrid) is XGrid xGrid)
                    o.XGrid = xGrid;
            }
        }

        execute(fumen.Taps.Where(x => x.ReferenceLaneStart is not null));
        execute(fumen.Holds.Where(x => x.ReferenceLaneStart is not null));
    }
}
