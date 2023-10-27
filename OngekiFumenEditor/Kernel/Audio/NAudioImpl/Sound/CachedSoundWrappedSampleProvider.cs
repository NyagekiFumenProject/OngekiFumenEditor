using NAudio.Wave;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound
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

		public int Read(float[] buffer, int offset, int count)
		{
			var beforePosition = position;
			for (int i = 0; i < count && position < cachedSound.AudioData.Length; i++)
				buffer[offset + i] = cachedSound.AudioData[position++];
			return position - beforePosition;
		}
	}
}
