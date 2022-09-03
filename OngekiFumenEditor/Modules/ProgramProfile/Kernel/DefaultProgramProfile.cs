using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.ProgramProfile.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.ProgramProfile.Kernel
{

    [Export(typeof(IEditorProfile))]
#if DEBUG
    public class DefaultProgramProfile : IEditorProfile
    {
        private static double NanosecPerTick { get; } = (1000 * 1000 * 1000) / Stopwatch.Frequency;

        class EditorProfileHandle : IProfileHandle
        {
            public FumenVisualEditorViewModel Editor { get; init; }
            public List<double> CallTicks { get; init; } = new();
            public Stopwatch Stopwatch { get; init; } = new Stopwatch();

            public bool IsProfiling { get; set; } = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IProfileHandle BeginEditorPlayProfile(FumenVisualEditorViewModel editor)
        {
            var handle = new EditorProfileHandle()
            {
                Editor = editor
            };

            handle.Stopwatch.Restart();
            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndEditorPlayProfile(IProfileHandle h)
        {
            if (h is not EditorProfileHandle handle)
                return;

            handle.Stopwatch.Stop();
            var sp = handle.CallTicks.IntervalBy((prev, cur) => cur - prev).ToArray();

            var aveTick = sp.Average();
            (var minTick, var maxTick) = sp.MaxMinBy(x => x);
            var group = sp.GroupBy(x => (int)x).OrderByDescending(x => x.Count()).Select(x => x.Key);

            Log.LogDebug($"ave : {(aveTick)}ms , min : {(minTick)}ms , max : {(maxTick)}ms");
            Log.LogDebug($"group: {string.Join("  ", group)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick(IProfileHandle h)
        {
            if (h is not EditorProfileHandle handle)
                return;

            handle.CallTicks.Add(handle.Stopwatch.ElapsedMilliseconds);
        }
    }
#endif
    public class EmptyProgramProfile : IEditorProfile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IProfileHandle BeginEditorPlayProfile(FumenVisualEditorViewModel editor)
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndEditorPlayProfile(IProfileHandle handle)
        {

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick(IProfileHandle profileHandle)
        {

        }
    }

}
