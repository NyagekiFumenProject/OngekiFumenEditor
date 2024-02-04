using IntervalTree;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
    public static class MathUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Random() => RandomHepler.RandomDouble();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int min, int max) => RandomHepler.Random(min, max);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int max) => RandomHepler.Random(max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Normalize(double from, double to, double cur)
        {
            var duration = to - from;
            var normalized = (cur - from) / duration;
            return normalized;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LCM(int a, int b)
        {
            return a / GCD(a, b) * b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GCD(int a, int b)
        {
            return b == 0 ? a : GCD(b, a % b);
        }

        public static double CalculateLength(TGrid from, TGrid to, BpmList bpmList)
        {
            var fromBpm = bpmList.GetBpm(from);
            var toBpm = bpmList.GetBpm(to);

            if (fromBpm == toBpm)
            {
                return CalculateBPMLength(fromBpm, to);
            }
            else
            {
                var nextBpm = bpmList.GetNextBpm(fromBpm);
                var pre = CalculateBPMLength(from, nextBpm.TGrid, fromBpm.BPM);
                var aft = CalculateBPMLength(toBpm.TGrid, to, toBpm.BPM);

                var mid = 0d;
                var cur = nextBpm;
                while (cur != toBpm)
                {
                    nextBpm = bpmList.GetNextBpm(cur);

                    //calc len
                    mid += CalculateBPMLength(cur.TGrid, nextBpm.TGrid, cur.BPM);

                    cur = nextBpm;
                }

                return pre + mid + aft;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RadianToAngle(float radian)
            => radian * 180 / MathF.PI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleToRadian(float angle)
            => angle * MathF.PI / 180;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateLength(XGrid from, XGrid to, double unitLen)
            => (to.TotalUnit - from.TotalUnit) * unitLen;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, BPMChange to)
            => CalculateBPMLength(from, to.TGrid);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, TGrid to)
            => CalculateBPMLength(from.TGrid, to, from.BPM);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(TGrid from, TGrid to, double bpm)
        {
            if (to is null)
                return double.PositiveInfinity;
            var msec = CalculateBPMLength(from.TotalUnit, to.TotalUnit, bpm);
            return msec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(double fromTGridUnit, double toTGridUnit, double bpm, uint resT = TGrid.DEFAULT_RES_T)
        {
            /*
            var size = bpm / 240 * timeGridSize;

            /**
             * 比如from是unit=50 grid=500 bpm = 240 resT=1000
             * to是unit=51 grid=0 bpm = 480
             * timeGridSize是1000
             * 那么
             * len = ((51 - 50) + (0 - 500) / 1000) * (240 / 240 * 1000)
             *     = (-500.0/1000 + 1) * 1000
             *     = 0.5 * 1000
             *     = 500
             *
            var diff = to - from;
            return (diff.Unit + diff.Grid * 1.0 / from.ResT) * size;
            */

            var diffGridUnit = toTGridUnit - fromTGridUnit;
            var totalGrid = diffGridUnit * resT;
            var msec = 240_000 * totalGrid / (resT * bpm);
            return msec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double Limit(double val, double min, double max)
        {
            if (min > max)
            {
                var t = min;
                min = max;
                max = t;
            }

            return Math.Max(Math.Min(val, max), min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<double, double> BuildTwoPointFormFormula(double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return y => x1;

            return y => (y - y1) * 1.0 / by * bx + x1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return x1;

            return (y - y1) * 1.0 / by * bx + x1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateYFromTwoPointFormFormula(double x, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return y1;

            return (x - x1) / bx * by + y1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(double start1, double end1, double start2, double end2) => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2) => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXFromBetweenObjects<T>(T from, T to, FumenVisualEditorViewModel editor, TGrid tGrid) where T : IHorizonPositionObject, ITimelineObject
            => CalculateXFromBetweenObjects(from.TGrid, from.XGrid, to.TGrid, to.XGrid, editor, tGrid);

        public static XGrid CalculateXGridFromBetweenObjects(TGrid fromTGrid, XGrid fromXGrid, TGrid toTGrid, XGrid toXGrid, TGrid tGrid)
        {
            var timeX = CalculateXFromTwoPointFormFormula(tGrid.TotalGrid, fromXGrid.TotalGrid, fromTGrid.TotalGrid, toXGrid.TotalGrid, toTGrid.TotalGrid);
            var xGrid = new XGrid((float)(timeX / fromXGrid.ResX), 0);
            xGrid.NormalizeSelf();

            return xGrid;
        }

        public static double CalculateXFromBetweenObjects(TGrid fromTGrid, XGrid fromXGrid, TGrid toTGrid, XGrid toXGrid, FumenVisualEditorViewModel editor, TGrid tGrid)
        {
            var xGrid = CalculateXGridFromBetweenObjects(fromTGrid, fromXGrid, toTGrid, toXGrid, tGrid);
            var timeX = XGridCalculator.ConvertXGridToX(xGrid, editor);
            return timeX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float calcGradient(float x1, float y1, float x2, float y2)
        {
            if (y1 == y2)
                return float.MaxValue;

            return (y1 - y2) / (x1 - x2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(T a, T b) where T : GridBase => a > b ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(T a, T b) where T : GridBase => a > b ? b : a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan Min(TimeSpan a, TimeSpan b) => a > b ? b : a;

        public static IEnumerable<int> GetIntegersBetweenTwoValues(double from, double to)
        {
            var sign = Math.Sign(to - from);
            var begin = 0;
            var end = 0;

            if (sign > 0)
            {
                begin = (int)Math.Ceiling(from);
                end = (int)Math.Floor(to);
            }
            if (sign < 0)
            {
                begin = (int)Math.Floor(from);
                end = (int)Math.Ceiling(to);
            }

            for (int i = begin; sign > 0 ? i <= end : i >= end; i += sign)
                yield return i;
        }

        public static IEnumerable<IEnumerable<T>> SplitByTurningGradient<T>(IEnumerable<T> collection, Func<T, float> valMapFunc)
        {
            float calcGradient(T a, T b)
            {
                var va = valMapFunc(a);
                var vb = valMapFunc(b);

                if (va == vb)
                    return float.MaxValue;

                return -(va - vb);
            }

            var itor = collection.GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var list = new List<T>();
            var prevPoint = itor.Current;
            var prevSign = 0;

            while (true)
            {
                if (!itor.MoveNext())
                    break;
                var point = itor.Current;
                var sign = MathF.Sign(calcGradient(prevPoint, point));

                if (prevSign != sign && list.Count != 0)
                {
                    yield return list;
                    list.Clear();
                    list.Add(prevPoint);
                }

                prevPoint = point;
                prevSign = sign;

                list.Add(point);
            }

            if (list.Count > 0)
                yield return list;
        }

        public record CombinableRange<T>(T Min, T Max) where T : IComparable<T>
        {
            public static IEnumerable<CombinableRange<T>> CombineRanges(IEnumerable<CombinableRange<T>> sortedList)
            {
                var itor = sortedList.OrderBy(x => x.Min).GetEnumerator();
                if (!itor.MoveNext())
                    yield break;
                var cur = itor.Current;
                while (itor.MoveNext())
                {
                    var next = itor.Current;
                    if (next.Min.CompareTo(cur.Max) <= 0)
                    {
                        //combinable
                        var newMin = cur.Min.CompareTo(next.Min) < 0 ? cur.Min : next.Min;
                        var newMax = cur.Max.CompareTo(next.Max) > 0 ? cur.Max : next.Max;

                        cur = new(newMin, newMax);
                    }
                    else
                    {
                        yield return cur;
                        cur = next;
                    }
                }
                if (cur is not null)
                    yield return cur;
            }

            public static IIntervalTree<T, CombinableRange<T>> ToIntervalTree(IEnumerable<CombinableRange<T>> sortedList)
            {
                var comparer = new ComparerWrapper<T>((a, b) => a.CompareTo(b));
                var tree = new IntervalTree<T, CombinableRange<T>>(comparer);

                foreach (var range in sortedList)
                    tree.Add(range.Min, range.Max, range);

                return tree;
            }
        }
    }
}
