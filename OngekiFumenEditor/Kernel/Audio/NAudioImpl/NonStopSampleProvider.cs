using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl
{
	internal class NonStopSampleProvider : ISampleProvider
	{
		private readonly WaveFormat format;

		public NonStopSampleProvider(WaveFormat format)
		{
			this.format = format;
		}

		public WaveFormat WaveFormat => format;

		public int Read(float[] buffer, int offset, int count)
		{
			Array.Clear(buffer, offset, count);
			return count;
		}
	}
}
