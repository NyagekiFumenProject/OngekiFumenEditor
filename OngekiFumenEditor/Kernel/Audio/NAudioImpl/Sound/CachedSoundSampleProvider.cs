using NAudio.Wave;
using System;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound
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

		public int Read(float[] buffer, int offset, int count)
		{
			var availableSamples = cachedSound.AudioData.Length - position;
			var samplesToCopy = Math.Min(availableSamples, count);
			Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
			position += samplesToCopy;
			return (int)samplesToCopy;
		}
	}
}
