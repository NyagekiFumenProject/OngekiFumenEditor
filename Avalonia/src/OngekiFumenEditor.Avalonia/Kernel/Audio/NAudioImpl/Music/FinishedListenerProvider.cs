using NAudio.Wave;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Music
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

		public int Read(Span<float> buffer)
		{
			var read = Provider.Read(buffer);
			if (read < buffer.Length && enableEventFire)
				OnReturnEmptySamples?.Invoke();

			if (read < buffer.Length)
				buffer[read..].Clear();

			read = buffer.Length;
			return read;
		}
	}
}


