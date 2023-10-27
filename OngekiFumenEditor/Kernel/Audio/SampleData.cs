using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Kernel.Audio
{
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
