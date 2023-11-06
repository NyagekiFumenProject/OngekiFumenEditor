using NAudio.Wave;
using System;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music
{
	internal class FinishedListenerProvider : ISampleProvider
	{
		private ISampleProvider provider;
		public ISampleProvider Provider => provider;
		public WaveFormat WaveFormat => provider.WaveFormat;
		private bool enableEventFire;

		public event Action OnReturnEmptySamples;

		public FinishedListenerProvider(ISampleProvider provider)
		{
			this.provider = provider;
		}

		public void StartListen()
		{
			enableEventFire = true;
		}

		public void StopListen()
		{
			enableEventFire = false;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			var read = Provider.Read(buffer, offset, count);
			if (read == 0 && enableEventFire)
				OnReturnEmptySamples?.Invoke();
			
			return read;
		}
	}
}
