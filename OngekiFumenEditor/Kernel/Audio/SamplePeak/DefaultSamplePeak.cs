using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Kernel.Audio.SamplePeak
{
	[Export(typeof(ISamplePeak))]
	internal class DefaultSamplePeak : ISamplePeak
	{
		const float SampleDurationPerPoint = 0.001f;

		public PeakPointCollection GetPeakValues(SampleData data)
		{
			var list = new PeakPointCollection(data.SampleInfo);

			var channels = data.SampleInfo.Channels;
			var samplesPerPoint = (int)(data.SampleInfo.SampleRate * SampleDurationPerPoint * channels);

			var floatBuffer = MemoryMarshal.Cast<byte, float>(data.Samples.Span);
			var samplesCount = floatBuffer.Length;

			//point count and resize at first.
			list.Capacity = samplesCount / samplesPerPoint;

			var time = TimeSpan.Zero;
			var appendTimeInterval = TimeSpan.FromSeconds(SampleDurationPerPoint);

			list.BeginBatchAction();
			for (int i = 0; i < samplesCount; i += samplesPerPoint)
			{
				var amplitudes = new float[channels];
				var subLength = Math.Min(samplesCount, i + samplesPerPoint);

				for (int j = i; j < subLength; j += channels)
				{
					for (int c = 0; c < channels; c++)
					{
						amplitudes[c] = Math.Max(amplitudes[c], floatBuffer[j + c]);
					}
				}

				time += appendTimeInterval;
				var point = new PeakPoint(time, amplitudes);
				list.Add(point);
			}
			list.EndBatchAction();

			return list;
		}
	}
}
