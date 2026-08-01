using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl
{
	internal class NonStopSampleProvider : ISampleProvider
	{
		private readonly WaveFormat format;

		public NonStopSampleProvider(WaveFormat format)
		{
			this.format = format;
		}

		public WaveFormat WaveFormat => format;

		public int Read(Span<float> buffer)
		{
			buffer.Clear();
			return buffer.Length;
		}
	}
}


