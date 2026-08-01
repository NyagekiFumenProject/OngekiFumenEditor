using NAudio.Wave;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Sound
{
	public class CachedSoundWrappedSampleProvider : ISampleProvider
	{
		private readonly CachedSound cachedSound;

		public CachedSoundWrappedSampleProvider(CachedSound cachedSound)
		{
			this.cachedSound = cachedSound;
		}

		public WaveFormat WaveFormat => cachedSound.WaveFormat;

		private int position = 0;

		public int Read(Span<float> buffer)
		{
			var count = Math.Min(buffer.Length, cachedSound.AudioData.Length - position);
			cachedSound.AudioData.AsSpan(position, count).CopyTo(buffer);
			position += count;
			return count;
		}
	}
}


