using Injectio.Attributes;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.SamplePeak;

[RegisterSingleton<ISamplePeak>]
internal class DefaultSamplePeak : ISamplePeak
{
    private const float SampleDurationPerPoint = 0.001f;

    public PeakPointCollection GetPeakValues(SampleData data)
    {
        var list = new PeakPointCollection(data.SampleInfo);

        var channels = data.SampleInfo.Channels;
        var samplesPerPoint = (int)(data.SampleInfo.SampleRate * SampleDurationPerPoint * channels);

        var floatBuffer = MemoryMarshal.Cast<byte, float>(data.Samples.Span);
        var samplesCount = floatBuffer.Length;
        list.Capacity = samplesCount / samplesPerPoint;

        var time = TimeSpan.Zero;
        var appendTimeInterval = TimeSpan.FromSeconds(SampleDurationPerPoint);

        list.BeginBatchAction();
        for (var i = 0; i < samplesCount; i += samplesPerPoint)
        {
            var amplitudes = new float[channels];
            var subLength = Math.Min(samplesCount, i + samplesPerPoint);

            for (var j = i; j < subLength; j += channels)
            {
                for (var c = 0; c < channels; c++)
                    amplitudes[c] = Math.Max(amplitudes[c], floatBuffer[j + c]);
            }

            time += appendTimeInterval;
            list.Add(new PeakPoint(time, amplitudes));
        }
        list.EndBatchAction();

        return list;
    }
}

