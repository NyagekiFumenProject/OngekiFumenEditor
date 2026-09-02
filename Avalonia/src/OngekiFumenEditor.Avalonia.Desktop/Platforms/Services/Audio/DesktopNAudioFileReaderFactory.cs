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
            ".mp3" => new AudioFileReader(filePath),
            ".wav" => CreateWaveFileReader(filePath),
            ".aif" or ".aiff" => new AiffFileReader(filePath),
            _ => new MediaFoundationReader(filePath)
        };

    public WaveStream CreateAudioFileReader(Stream stream, AudioStreamFormat format)
        => format switch
        {
            AudioStreamFormat.Wav => CreateWaveFileReader(stream),
            AudioStreamFormat.Aiff => new AiffFileReader(stream),
            AudioStreamFormat.Mp3 => new AudioFileReader(stream),
            AudioStreamFormat.Acb => throw new PlatformNotSupportedException(
                "ACB audio must be converted before creating an audio reader."),
            _ => throw new NotSupportedException($"Unsupported audio stream format: {format}")
        };

    private static WaveStream CreateWaveFileReader(string filePath)
    {
        WaveStream reader = new WaveFileReader(filePath);
        return EnsureSupportedWaveFormat(reader);
    }

    private static WaveStream CreateWaveFileReader(Stream stream)
    {
        WaveStream reader = new WaveFileReader(stream);
        return EnsureSupportedWaveFormat(reader);
    }

    private static WaveStream EnsureSupportedWaveFormat(WaveStream reader)
    {
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
