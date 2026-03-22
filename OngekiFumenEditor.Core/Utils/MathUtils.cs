using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils;

public static partial class MathUtils
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
        return (cur - from) / duration;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LCM(int a, int b) => a / GCD(a, b) * b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);

    public static Vector2? GetLinesIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        const float epsilon = 1e-6f;
        if (p1 == q1)
            return p1;
        if (p2 == q2)
            return p2;

        var r = new Vector2(p2.X - p1.X, p2.Y - p1.Y);
        var s = new Vector2(q2.X - q1.X, q2.Y - q1.Y);
        var crossRS = r.X * s.Y - r.Y * s.X;

        if (Math.Abs(crossRS) < epsilon)
            return null;

        var t = ((q1.X - p1.X) * s.Y - (q1.Y - p1.Y) * s.X) / crossRS;
        var u = ((q1.X - p1.X) * r.Y - (q1.Y - p1.Y) * r.X) / crossRS;

        if (t >= -epsilon && t <= 1 + epsilon && u >= -epsilon && u <= 1 + epsilon)
        {
            t = Math.Clamp(t, 0, 1);
            return new(p1.X + t * r.X, p1.Y + t * r.Y);
        }

        return null;
    }

    public static double CalculateLength(TGrid from, TGrid to, BpmList bpmList)
    {
        var fromBpm = bpmList.GetBpm(from);
        var toBpm = bpmList.GetBpm(to);

        if (fromBpm == toBpm)
            return CalculateBPMLength(fromBpm, to);

        var nextBpm = bpmList.GetNextBpm(fromBpm);
        var pre = CalculateBPMLength(from, nextBpm.TGrid, fromBpm.BPM);
        var aft = CalculateBPMLength(toBpm.TGrid, to, toBpm.BPM);

        var mid = 0d;
        var cur = nextBpm;
        while (cur != toBpm)
        {
            nextBpm = bpmList.GetNextBpm(cur);
            mid += CalculateBPMLength(cur.TGrid, nextBpm.TGrid, cur.BPM);
            cur = nextBpm;
        }

        return pre + mid + aft;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float RadianToAngle(float radian) => radian * 180 / MathF.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AngleToRadian(float angle) => angle * MathF.PI / 180;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateLength(XGrid from, XGrid to, double unitLen) => (to.TotalUnit - from.TotalUnit) * unitLen;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateBPMLength(BPMChange from, BPMChange to) => CalculateBPMLength(from, to.TGrid);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateBPMLength(BPMChange from, TGrid to) => CalculateBPMLength(from.TGrid, to, from.BPM);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateBPMLength(TGrid from, TGrid to, double bpm)
    {
        if (to is null)
            return double.PositiveInfinity;

        return CalculateBPMLength(from.TotalUnit, to.TotalUnit, bpm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateBPMLength(double fromTGridUnit, double toTGridUnit, double bpm,
        uint resT = TGrid.DEFAULT_RES_T, uint timeT = 240_000)
    {
        var diffGridUnit = toTGridUnit - fromTGridUnit;
        var totalGrid = diffGridUnit * resT;
        return timeT * totalGrid / (resT * bpm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Limit(double val, double min, double max)
    {
        if (min > max)
            (min, max) = (max, min);

        return Math.Max(Math.Min(val, max), min);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Func<double, double> BuildTwoPointFormFormula(double x1, double y1, double x2, double y2)
    {
        var by = y2 - y1;
        var bx = x2 - x1;

        if (by == 0)
            return _ => x1;

        return y => (y - y1) / by * bx + x1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
    {
        var by = y2 - y1;
        var bx = x2 - x1;

        if (by == 0)
            return x1;

        return (y - y1) / by * bx + x1;
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
    public static bool IsInRange(double start1, double end1, double start2, double end2)
        => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
        => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);

    public static XGrid CalculateXGridFromBetweenObjects(TGrid fromTGrid, XGrid fromXGrid, TGrid toTGrid, XGrid toXGrid, TGrid tGrid)
    {
        var timeX = CalculateXFromTwoPointFormFormula(tGrid.TotalGrid, fromXGrid.TotalGrid, fromTGrid.TotalGrid, toXGrid.TotalGrid, toTGrid.TotalGrid);
        var xGrid = new XGrid((float)(timeX / fromXGrid.ResX));
        xGrid.NormalizeSelf();
        return xGrid;
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

        for (var i = begin; sign > 0 ? i <= end : i >= end; i += sign)
            yield return i;
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = (x - edge0) / (edge1 - edge0);
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = (x - edge0) / (edge1 - edge0);
        t = Math.Clamp(t, 0d, 1d);
        return t * t * (3d - 2d * t);
    }

    public record CombinableRange<T>(T Min, T Max) where T : IComparable<T>
    {
        public static IEnumerable<CombinableRange<T>> CombineRanges(IEnumerable<CombinableRange<T>> sortedList)
        {
            using var itor = sortedList.OrderBy(x => x.Min).GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var cur = itor.Current;
            while (itor.MoveNext())
            {
                var next = itor.Current;
                if (next.Min.CompareTo(cur.Max) <= 0)
                {
                    var newMin = cur.Min.CompareTo(next.Min) < 0 ? cur.Min : next.Min;
                    var newMax = cur.Max.CompareTo(next.Max) > 0 ? cur.Max : next.Max;
                    cur = new CombinableRange<T>(newMin, newMax);
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
