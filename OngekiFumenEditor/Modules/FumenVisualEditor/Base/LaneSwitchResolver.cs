using System;
using System.Collections.Generic;
using System.Linq;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    // 仅在物件拖动期间复用；轨道自身参与移动时不使用该缓存。
    internal sealed class LaneSwitchResolver
    {
        internal const double SnapDistance = LaneGeometry.LaneSnapDistancePixels;
        private readonly Func<TGrid, IEnumerable<LaneStartBase>> queryLanes;
        private readonly Func<XGrid, double> displayX;
        private readonly Dictionary<int, LaneStartBase[]> candidates = new();
        private readonly Dictionary<LaneStartBase, Func<TGrid, XGrid, XGrid>> projectors = new();
        private readonly Dictionary<LaneStartBase, bool> validPaths = new();

        internal LaneSwitchResolver(Func<TGrid, IEnumerable<LaneStartBase>> queryLanes, Func<XGrid, double> displayX)
        {
            this.queryLanes = queryLanes;
            this.displayX = displayX;
        }

        internal XGrid Project(LaneStartBase lane, TGrid time, XGrid target)
        {
            if (lane == null)
                return null;
            if (!projectors.TryGetValue(lane, out var project))
                projectors[lane] = project = LaneGeometry.CreateProjector(lane);
            return project(time, target);
        }

        internal LaneStartBase Resolve(LaneStartBase current, TGrid time, XGrid target, TGrid endTime = null)
        {
            if (!candidates.TryGetValue(time.TotalGrid, out var lanes))
            {
                // 复用轨道时间索引，不遍历全谱音符。同一时刻的群体拖动共享查询。
                lanes = queryLanes(time)
                    .Where(l => l.IsDockableLane).Distinct().ToArray();
                if (candidates.Count >= 256)
                    candidates.Clear();
                candidates[time.TotalGrid] = lanes;
            }

            var wantedX = displayX(target);
            var best = current;
            var bestDistance = SnapDistance + .000001;
            // 距离相同时保留当前轨道，避免交叉点附近抖动。
            var currentX = Project(current, time, target);
            if (currentX != null)
                bestDistance = Math.Min(bestDistance, Math.Abs(displayX(currentX) - wantedX));
            foreach (var lane in lanes)
            {
                if (ReferenceEquals(lane, current))
                    continue;
                if (endTime != null)
                {
                    if (endTime < time || time < lane.MinTGrid || endTime > lane.MaxTGrid)
                        continue;
                    if (!validPaths.TryGetValue(lane, out var valid))
                        validPaths[lane] = valid = lane.IsPathVaild();
                    if (!valid || Project(lane, endTime, target) == null)
                        continue;
                }

                var x = Project(lane, time, target);
                if (x == null)
                    continue;
                var distance = Math.Abs(displayX(x) - wantedX);
                if (distance > SnapDistance || distance >= bestDistance - .000001)
                    continue;
                best = lane;
                bestDistance = distance;
            }

            return best;
        }
    }
}
