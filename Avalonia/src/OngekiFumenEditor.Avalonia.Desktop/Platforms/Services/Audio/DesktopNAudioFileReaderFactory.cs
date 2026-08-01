using Injectio.Attributes;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using System;
using System.Collections.Generic;
using System.IO;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Audio;

[RegisterSingleton<INAudioFileReaderFactory>]
internal sealed class DesktopNAudioFileReaderFactory : INAudioFileReaderFactory
{
    public IReadOnlyList<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } =
    [
        (".mp3", "Audio File"),
        (".wav", "Audio File"),
        (".aif", "Audio File"),
        (".aiff", "Audio File"),
        (".acb", "Criware Audio File")
    ];

    public WaveStream CreateAudioFileReader(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".wav" => CreateWaveFileReader(filePath),
            ".aif" or ".aiff" => new AiffFileReader(filePath),
            _ => new MediaFoundationReader(filePath)
        };

    private static WaveStream CreateWaveFileReader(string filePath)
    {
        WaveStream reader = new WaveFileReader(filePath);
        var format = reader.WaveFormat.AsStandardWaveFormat();
        if (format.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat)
            return reader;

#if NATIVE_AOT
        reader.Dispose();
        throw new NotSupportedException(
            "Native-AOT builds support PCM and IEEE-float WAV files. Use MP3 or convert the WAV to PCM.");
#else
        reader = WaveFormatConversionStream.CreatePcmStream(reader);
        return new BlockAlignReductionStream(reader);
#endif
    }
}
