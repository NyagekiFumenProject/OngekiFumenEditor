using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Sound
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
			Duration = CalculateDuration(AudioData, WaveFormat);
		}

		public CachedSound(float[] newBuf, WaveFormat outFormat)
		{
			AudioData = newBuf;
			WaveFormat = outFormat;
			Duration = CalculateDuration(AudioData, WaveFormat);
		}

		private static TimeSpan CalculateDuration(float[] audioData, WaveFormat waveFormat)
		{
			if (waveFormat.SampleRate <= 0 || waveFormat.Channels <= 0)
				return TimeSpan.Zero;

			return TimeSpan.FromSeconds(
				(double)audioData.LongLength / waveFormat.SampleRate / waveFormat.Channels);
		}

		public ISampleProvider CreateSampleProvider()
		{
			return new CachedSoundWrappedSampleProvider(this);
		}
	}
}


