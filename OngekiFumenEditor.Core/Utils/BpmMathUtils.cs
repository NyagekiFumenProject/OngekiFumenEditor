using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
    public static class BpmMathUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, BPMChange to)
        {
            return CalculateBPMLength(from, to.TGrid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, TGrid to)
        {
            return CalculateBPMLength(from.TGrid, to, from.BPM);
        }

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
            var msec = timeT * totalGrid / (resT * bpm);
            return msec;
        }
    }
}
