using System;
using System.Collections.Generic;
using System.Linq;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public static class LaneGeometry
    {
        public static Func<TGrid, XGrid, XGrid> CreateProjector(LaneStartBase lane)
        {
            var children = lane.Children.ToArray();
            var valid = lane.IsPathVaild();
            var horizontalRanges = new Dictionary<int, List<(int min, int max)>>();
            foreach (var child in children)
            {
                if (child.PrevObject is not OngekiMovableObjectBase previous || previous.TGrid.TotalGrid != child.TGrid.TotalGrid)
                    continue;
                var t = child.TGrid.TotalGrid;
                var min = Math.Min(previous.XGrid.TotalGrid, child.XGrid.TotalGrid);
                var max = Math.Max(previous.XGrid.TotalGrid, child.XGrid.TotalGrid);
                var horizontal = true;
                foreach (var control in child.PathControls)
                {
                    if (control.TGrid.TotalGrid != t)
                    {
                        horizontal = false;
                        break;
                    }

                    min = Math.Min(min, control.XGrid.TotalGrid);
                    max = Math.Max(max, control.XGrid.TotalGrid);
                }

                if (!horizontal)
                    continue;
                if (child.PathControls.Count > 0)
                {
                    var path = child.GetConnectionPaths().Select(p => p.pos.X).ToArray();
                    if (path.Length > 0)
                    {
                        min = (int)Math.Ceiling(path.Min());
                        max = (int)Math.Floor(path.Max());
                    }
                }

                if (!horizontalRanges.TryGetValue(t, out var ranges))
                    horizontalRanges[t] = ranges = new();
                ranges.Add((min, max));
            }

            var interpolated = new Dictionary<int, XGrid>();
            return (time, target) =>
            {
                if (horizontalRanges.TryGetValue(time.TotalGrid, out var ranges))
                {
                    int closest = target.TotalGrid;
                    long distance = long.MaxValue;
                    foreach (var range in ranges)
                    {
                        var x = Math.Max(range.min, Math.Min(range.max, target.TotalGrid));
                        var delta = Math.Abs((long)x - target.TotalGrid);
                        if (delta >= distance)
                            continue;
                        distance = delta;
                        closest = x;
                    }

                    return XGrid.FromTotalGrid(closest);
                }

                if (interpolated.TryGetValue(time.TotalGrid, out var cached))
                    return cached;
                XGrid result = null;
                if (!valid)
                    result = lane.CalulateXGrid(time);
                else if (time >= lane.TGrid && children.Length > 0)
                {
                    var lo = 0;
                    var hi = children.Length;
                    while (lo < hi)
                    {
                        var mid = lo + (hi - lo) / 2;
                        if (children[mid].TGrid < time)
                            lo = mid + 1;
                        else
                            hi = mid;
                    }

                    if (lo < children.Length)
                        result = children[lo].CalulateXGrid(time);
                }

                interpolated[time.TotalGrid] = result;
                return result;
            };
        }

    }
}
