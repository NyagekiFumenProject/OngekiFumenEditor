using System;

namespace NAudio.Wave;

internal static class ISampleProviderExtensions
{
    public static int Read(
        this ISampleProvider sampleProvider,
        float[] buffer,
        int offset,
        int count)
        => sampleProvider is ILegacyArraySampleProvider legacyProvider
            ? legacyProvider.Read(buffer, offset, count)
            : sampleProvider.Read(buffer.AsSpan(offset, count));
}
