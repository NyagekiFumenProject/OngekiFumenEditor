using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        /// <summary>WAVE_FORMAT_EXTENSIBLE 的 KSDATAFORMAT_SUBTYPE_PCM。</summary>
        private static readonly Guid SubFormatPcm = new("00000001-0000-0010-8000-00aa00389b71");

        /// <summary>
        /// 已按目标采样率与双声道处理完的采样流；读完（或出错）后由调用方 Dispose，释放底层文件句柄。
        /// </summary>
        internal sealed class ProcessedSampleProvider : ISampleProvider, IDisposable
        {
            private readonly ISampleProvider provider;
            private readonly IDisposable underlyingReader;

            internal ProcessedSampleProvider(ISampleProvider provider, IDisposable underlyingReader, TimeSpan duration)
            {
                this.provider = provider;
                this.underlyingReader = underlyingReader;
                Duration = duration;
            }

            public TimeSpan Duration { get; }
            public WaveFormat WaveFormat => provider.WaveFormat;
            public int Read(float[] buffer, int offset, int count) => provider.Read(buffer, offset, count);
            public void Dispose() => underlyingReader.Dispose();
        }

        /// <summary>
        /// 打开音频文件并做格式兼容（目标采样率、双声道），返回可释放的采样流。
        /// 首选托管路径：<see cref="WaveFileReader"/> + 按位深的 PCM/IEEE float 转换器，不依赖 ACM。
        /// 之所以不用 <see cref="AudioFileReader"/> 作首选：它对 WAVE_FORMAT_EXTENSIBLE（例如 24bit PCM
        /// 的 wav）会走 ACM 的 acmFormatSuggest，缺 ACM 驱动的机器上直接 "NoDriver calling acmFormatSuggest"
        /// 失败，音乐/音效和波形频谱就都没有数据了。托管路径覆盖不到的格式（ADPCM、µ-law、mp3 等）仍回退到
        /// <see cref="AudioFileReader"/>。
        /// </summary>
        public static async Task<ProcessedSampleProvider> OpenSampleProvider(string audioFile, int targetSampleRate, CancellationToken cancellationToken = default)
        {
            IDisposable reader;
            ISampleProvider provider;
            TimeSpan duration;

            if (TryOpenWithManagedConverters(audioFile, out var waveReader, out var samples))
            {
                reader = waveReader;
                provider = samples;
                duration = waveReader.TotalTime;
            }
            else
            {
                var audioFileReader = new AudioFileReader(audioFile);
                reader = audioFileReader;
                provider = audioFileReader;
                duration = audioFileReader.TotalTime;
            }

            try
            {
                var processed = await CheckCompatible(provider, targetSampleRate, cancellationToken);
                return new ProcessedSampleProvider(processed, reader, duration);
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        private static bool TryOpenWithManagedConverters(string audioFile, out WaveFileReader reader, out ISampleProvider provider)
        {
            reader = null;
            provider = null;

            WaveFileReader opened;
            try
            {
                opened = new WaveFileReader(audioFile);
            }
            catch (Exception)
            {
                return false;
            }

            var samples = TryConvertToSamples(opened);
            if (samples is null)
            {
                opened.Dispose();
                return false;
            }

            reader = opened;
            provider = samples;
            return true;
        }

        private static ISampleProvider TryConvertToSamples(IWaveProvider provider)
        {
            var format = provider.WaveFormat;

            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
                return new WaveToSampleProvider(provider);

            if (format is WaveFormatExtensible extensible && extensible.SubFormat != SubFormatPcm)
                return null;

            if (format.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.Extensible))
                return null;

            return format.BitsPerSample switch
            {
                8 => new Pcm8BitToSampleProvider(provider),
                16 => new Pcm16BitToSampleProvider(provider),
                24 => new Pcm24BitToSampleProvider(provider),
                32 => new Pcm32BitToSampleProvider(provider),
                _ => null,
            };
        }

        public static async Task<ISampleProvider> CheckCompatible(ISampleProvider waveProvider, int targetSampleRate, CancellationToken cancellationToken = default)
        {
            var outProvider = waveProvider;

            if (outProvider.WaveFormat.SampleRate != targetSampleRate)
            {
                Log.LogWarn($"Resample sound audio file from {outProvider.WaveFormat.SampleRate} to {targetSampleRate}");
                cancellationToken.ThrowIfCancellationRequested();
                outProvider = await Task.Run(() => ResampleCacheSound(outProvider, targetSampleRate));
            }

            if (outProvider.WaveFormat.Channels == 1)
            {
                Log.LogWarn($"Extend channel from Mono to Stereo");
                cancellationToken.ThrowIfCancellationRequested();
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
