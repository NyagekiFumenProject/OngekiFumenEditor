using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils
{
	internal class AudioCompatibilizer
	{
		private class BufferSampleProvider : ISampleProvider
		{
			private readonly float[] buffer;
			private readonly WaveFormat format;

			public BufferSampleProvider(float[] buffer, WaveFormat format)
			{
				this.buffer = buffer;
				this.format = format;
			}

			public WaveFormat WaveFormat => format;

			private int position = 0;

			public int Read(float[] buffer, int offset, int count)
			{
				var beforePosition = position;
				for (int i = 0; i < count && position < this.buffer.Length; i++)
					buffer[offset + i] = this.buffer[position++];
				return position - beforePosition;
			}
		}

		public static async Task<ISampleProvider> CheckCompatible(ISampleProvider waveProvider, int targetSampleRate)
		{
			var outProvider = waveProvider;

			if (outProvider.WaveFormat.SampleRate != targetSampleRate)
			{
				Log.LogWarn($"Resample sound audio file from {outProvider.WaveFormat.SampleRate} to {targetSampleRate}");
				outProvider = await Task.Run(() => ResampleCacheSound(outProvider, targetSampleRate));
			}

			if (outProvider.WaveFormat.Channels == 1)
			{
				Log.LogWarn($"Extend channel from Mono to Stereo");
				outProvider = await Task.Run(() => MonoToStereoSound(outProvider));
			}

			return outProvider;
		}

		private static ISampleProvider ResampleCacheSound(ISampleProvider outProvider, int targetSampleRate)
		{
			var resampler = new WdlResamplingSampleProvider(outProvider, targetSampleRate);
			var outFormat = resampler.WaveFormat;
			return new BufferSampleProvider(resampler.ToArray(), outFormat);
		}

		private static ISampleProvider MonoToStereoSound(ISampleProvider outProvider)
		{
			var converter = new MonoToStereoSampleProvider(outProvider);
			return new BufferSampleProvider(converter.ToArray(), converter.WaveFormat);
		}
	}
}
