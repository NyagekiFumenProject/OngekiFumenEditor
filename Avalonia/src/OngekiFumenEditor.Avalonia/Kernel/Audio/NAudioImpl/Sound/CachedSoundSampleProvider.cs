using NAudio.Wave;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Sound
{
	public class CachedSoundSampleProvider : ISampleProvider
	{
		private readonly CachedSound cachedSound;
		private long position;

		public WaveFormat WaveFormat => cachedSound.WaveFormat;

		public CachedSoundSampleProvider(CachedSound cachedSound)
		{
			this.cachedSound = cachedSound;
		}

		public int Read(Span<float> buffer)
		{
			var availableSamples = cachedSound.AudioData.Length - position;
			var samplesToCopy = (int)Math.Min(availableSamples, buffer.Length);
			cachedSound.AudioData.AsSpan((int)position, samplesToCopy).CopyTo(buffer);
			position += samplesToCopy;
			return samplesToCopy;
		}
	}
}


