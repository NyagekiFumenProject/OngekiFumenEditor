using NAudio.Wave;
using NWaves.Operations;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound
{
	public class CachedSound
	{
		public float[] AudioData { get; init; }
		public WaveFormat WaveFormat { get; init; }
		public TimeSpan Duration { get; init; }

		public CachedSound(ISampleProvider copySourceProvider)
		{
			AudioData = copySourceProvider.ToArray();
			WaveFormat = copySourceProvider.WaveFormat;
		}

		public CachedSound(float[] newBuf, WaveFormat outFormat)
		{
			AudioData = newBuf;
			WaveFormat = outFormat;
		}

		public ISampleProvider CreateSampleProvider()
		{
			return new CachedSoundWrappedSampleProvider(this);
		}
	}
}
