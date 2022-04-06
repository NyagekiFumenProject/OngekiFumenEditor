using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
    public static class MathUtils
    {
        private static Random rand = new Random();

        public static double Random() => rand.NextDouble();
        public static int Random(int min, int max) => rand.Next(min, max);
        public static int Random(int max) => rand.Next(max);

        public static double CalculateLength(TGrid from, TGrid to, BpmList bpmList, double unitLen)
        {
            var fromBpm = bpmList.GetBpm(from);
            var toBpm = bpmList.GetBpm(to);

            if (fromBpm == toBpm)
            {
                return CalculateBPMLength(fromBpm, to, unitLen);
            }
            else
            {
                var nextBpm = bpmList.GetNextBpm(fromBpm);
                var pre = CalculateBPMLength(from, nextBpm.TGrid, fromBpm.BPM, unitLen);
                var aft = CalculateBPMLength(toBpm.TGrid, to, toBpm.BPM, unitLen);

                var mid = 0d;
                var cur = nextBpm;
                while (cur != toBpm)
                {
                    nextBpm = bpmList.GetNextBpm(cur);

                    //calc len
                    mid += CalculateBPMLength(cur.TGrid, nextBpm.TGrid, cur.BPM, unitLen);

                    cur = nextBpm;
                }

                return pre + mid + aft;
            }
        }

        public static float RadianToAngle(float radian)
        {
            return radian * 180 / MathF.PI;
        }

        public static float AngleToRadian(float angle)
        {
            return angle * MathF.PI / 180;
        }

        public static double CalculateLength(XGrid from, XGrid to, double unitLen)
        {
            var diff = to - from;
            return (diff.Unit + diff.Grid / from.ResX) * unitLen;
        }

        public static double CalculateBPMLength(BPMChange from, BPMChange to, double timeGridSize)
        {
            return CalculateBPMLength(from, to.TGrid, timeGridSize);
        }

        public static double CalculateBPMLength(BPMChange from, TGrid to, double timeGridSize) => CalculateBPMLength(from.TGrid, to, from.BPM, timeGridSize);

        public static double CalculateBPMLength(TGrid from, TGrid to, double bpm, double timeGridSize)
        {
            if (to is null)
                return double.PositiveInfinity;
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

            var diff = to - from;
            var totalGrid = diff.Unit * from.ResT + diff.Grid;
            var msec = 240_000 * totalGrid / (from.ResT * bpm);
            return msec;
        }

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

        public static Func<double, double> BuildTwoPointFormFormula(double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return y => x1;

            return y => (y - y1) * 1.0 / by * bx + x1;
        }

        public static double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return x1;

            return (y - y1) * 1.0 / by * bx + x1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXFromBetweenObjects<T>(T from, T to, FumenVisualEditorViewModel editor, TGrid tGrid) where T : IHorizonPositionObject, ITimelineObject
            => CalculateXFromBetweenObjects(from.TGrid, from.XGrid, to.TGrid, to.XGrid, editor, tGrid);

        public static XGrid CalculateXGridFromBetweenObjects(TGrid fromTGrid, XGrid fromXGrid, TGrid toTGrid, XGrid toXGrid, TGrid tGrid)
        {
            var prevX = fromXGrid.TotalGrid;
            var prevY = fromTGrid.TotalGrid;
            var curX = toXGrid.TotalGrid;
            var curY = toTGrid.TotalGrid;

            var timeY = tGrid.TotalGrid;

            var timeX = CalculateXFromTwoPointFormFormula(timeY, prevX, prevY, curX, curY);
            var xGrid = new XGrid((float)(timeX / fromXGrid.ResX), 0, fromXGrid.ResX);
            xGrid.NormalizeSelf();

            return xGrid;
        }

        public static double CalculateXFromBetweenObjects(TGrid fromTGrid, XGrid fromXGrid, TGrid toTGrid, XGrid toXGrid, FumenVisualEditorViewModel editor, TGrid tGrid)
        {
            var xGrid = CalculateXGridFromBetweenObjects(fromTGrid, fromXGrid, toTGrid, toXGrid, tGrid);
            var timeX = XGridCalculator.ConvertXGridToX(xGrid, editor);
            return timeX;
        }

        public static float calcGradient(float x1, float y1, float x2, float y2)
        {
            if (y1 == y2)
                return float.MaxValue;

            return (y1 - y2) / (x1 - x2);
        }

        public static Vector BezierQuadratic(Vector start, Vector midControl, Vector end, float T)
        {
            float A = 1 - T;
            float B = T;
            float A_2 = A * A;
            float AB2 = A * B * 2;
            float B_2 = B * B;

            return new(start.X * A_2 + midControl.X * AB2 + end.X * B_2, start.Y * A_2 + midControl.Y * AB2 + end.Y * B_2);
        }

        public static T Max<T>(T a, T b) where T : GridBase => a > b ? a : b;
        public static T Min<T>(T a, T b) where T : GridBase => a > b ? b : a;

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
    }
}
