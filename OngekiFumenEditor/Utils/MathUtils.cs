using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class MathUtils
    {
        private static Random rand = new Random();

        public static double Random() => rand.NextDouble();
        public static int Random(int min, int max) => rand.Next(min, max);
        public static int Random(int max) => rand.Next(max);

        public static double CalculateLength(TGrid from, TGrid to, double unitLen)
        {
            var diff = to - from;
            return (diff.Unit + diff.Grid * 1.0 / from.ResT) * unitLen;
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

        public static double CalculateBPMLength(BPMChange from, TGrid to, double timeGridSize)
        {
            if (to is null)
                return double.PositiveInfinity;

            var size = from.BPM / 240 * timeGridSize;

            /**
             * 比如from是unit=50 grid=500 bpm = 240 resT=1000
             * to是unit=51 grid=0 bpm = 480
             * timeGridSize是1000
             * 那么
             * len = ((51 - 50) + (0 - 500) / 1000) * (240 / 240 * 1000)
             *     = (-500.0/1000 + 1) * 1000
             *     = 0.5 * 1000
             *     = 500
             */
            return CalculateLength(from.TGrid, to, size);
        }
    }
}
