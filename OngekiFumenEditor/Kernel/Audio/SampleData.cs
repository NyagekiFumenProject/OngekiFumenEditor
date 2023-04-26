using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace OngekiFumenEditor.Kernel.Audio
{
    public record SampleInfo()
    {
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }

        public int BytesPerSample => BitsPerSample / 8;
    }

    public record SampleData(ReadOnlyMemory<byte> Samples, SampleInfo SampleInfo)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T SafeGetSampleValue<T>(int i) where T : struct
            => (i < 0 || i >= Samples.Length) ? default : GetSampleValue<T>(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetSampleValue<T>(int i) where T : struct
            => MemoryMarshal.Cast<byte, T>(Samples.Span)[i];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CalculateSampleIndex(TimeSpan time)
            => (int)(time.TotalSeconds * SampleInfo.SampleRate * SampleInfo.BitsPerSample * SampleInfo.Channels);
    }
}
