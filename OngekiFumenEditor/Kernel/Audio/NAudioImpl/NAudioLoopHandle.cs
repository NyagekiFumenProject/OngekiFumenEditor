using NAudio.Wave.SampleProviders;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl
{
	public class NAudioLoopHandle : ILoopHandle
	{
		public float Volume
		{
			get => sampleProvider.Volume;
			set => sampleProvider.Volume = value;
		}

		public VolumeSampleProvider Provider => sampleProvider;

		private readonly VolumeSampleProvider sampleProvider;

		public NAudioLoopHandle(VolumeSampleProvider sampleProvider)
		{
			this.sampleProvider = sampleProvider;
		}
	}
}
