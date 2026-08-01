using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NAudio.BrowserAudioWorklet.Tests")]

namespace NAudio.Wave;

internal interface ILegacyArraySampleProvider
{
    int Read(float[] buffer, int offset, int count);
}
