using Caliburn.Micro;
using Gemini.Framework.Services;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Base;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Music;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Sound;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl
{
    [Export(typeof(IAudioManager))]
    internal class BassManager : PropertyChangedBase, IAudioManager
    {
        public const int OUTPUT_SAMPLES = 48000;

        private int soundMixVolumeHandle = 0;
        private FXVolumeParam volumeParam = new FXVolumeParam();

        public float SoundVolume
        {
            get
            {
                //var volume = Bass.ChannelGetAttribute(soundMixer, ChannelAttribute.Volume);
                //return (float)volume;
                Bass.FXGetParameters(soundMixVolumeHandle, volumeParam);
                return volumeParam.fCurrent;
            }

            set
            {
                //Bass.ChannelSetAttribute(soundMixer, ChannelAttribute.Volume, value);

                volumeParam.fCurrent = value;
                volumeParam.fTarget = value;
                volumeParam.fTime = 0;
                Bass.FXSetParameters(soundMixVolumeHandle, volumeParam);

                NotifyOfPropertyChange(() => SoundVolume);
            }
        }

        private int audioLatency;

        //private int masterMixer = 0;
        private int soundMixer = 0;

        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
            (".mp3","音频文件"),
            (".wav","音频文件"),
            (".acb","Criware音频文件"),
        };

        public BassManager()
        {
            WindowInteropHelper helper = new WindowInteropHelper(App.Current.MainWindow);
            var handle = helper.Handle;

            Bass.Init(-1, OUTPUT_SAMPLES, DeviceInitFlags.Latency, handle, default);
            BassUtils.ReportError(nameof(Bass.Init));

            Bass.GetInfo(out var info);
            BassUtils.ReportError(nameof(Bass.GetInfo));

            //audioLatency = info.Latency / 1000;//convert to seconds
            audioLatency = 0;

            soundMixer = BassMix.CreateMixerStream(OUTPUT_SAMPLES, 2, BassFlags.MixerNonStop);
            BassUtils.ReportError(nameof(BassMix.CreateMixerStream));

            Bass.ChannelPlay(soundMixer);
            BassUtils.ReportError(nameof(Bass.ChannelPlay));

            soundMixVolumeHandle = Bass.ChannelSetFX(soundMixer, (EffectType)9, 0);
            BassUtils.ReportError(nameof(Bass.ChannelSetFX));

            SoundVolume = 1;

            void config(Configuration cfg, int val)
            {
                var before = Bass.GetConfig(cfg);
                Bass.Configure(cfg, val);
                Log.LogDebug($"Configure Bass {cfg}: {before} -> {val} {(val == before ? "(same)" : string.Empty)}");
            }

            config(Configuration.UpdatePeriod, 16);
            config(Configuration.DevicePeriod, 10);
            config(Configuration.UpdateThreads, 2);
        }

        public void Dispose()
        {
            if (soundMixVolumeHandle != 0)
                Bass.ChannelRemoveFX(soundMixer, soundMixVolumeHandle);
            soundMixVolumeHandle = 0;

            if (soundMixer != 0)
                Bass.StreamFree(soundMixer);
            soundMixer = 0;

            /*
            if (Bass.Free())
                BassUtils.ReportError(nameof(Bass.Free));
            */
        }

        public void DumpSampleDataAndInfo(byte[] fileBuffer, out float[] samples, out SampleInfo info)
        {
            var handle = Bass.CreateStream(fileBuffer, 0, fileBuffer.Length, BassFlags.Decode | BassFlags.Float);
            BassUtils.ReportError(nameof(Bass.CreateStream));
            var byteLength = (int)Bass.ChannelGetLength(handle);
            BassUtils.ReportError(nameof(Bass.ChannelGetLength));
            Bass.ChannelGetInfo(handle, out var channelInfo);
            BassUtils.ReportError(nameof(Bass.ChannelGetData));
            var floatLength = byteLength / (channelInfo.OriginalResolution / 8) * channelInfo.Channels;
            samples = new float[floatLength];

            var read = Bass.ChannelGetData(handle, samples, floatLength);
            BassUtils.ReportError(nameof(Bass.ChannelGetData));

            info = new SampleInfo();
            info.Channels = channelInfo.Channels;
            info.SampleRate = channelInfo.Frequency;
            info.BitsPerSample = channelInfo.OriginalResolution;

            Bass.StreamFree(handle);
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            if (filePath.EndsWith(".acb"))
                filePath = await AcbConverter.ConvertAcbFileToWavFile(filePath);

            var buffer = await File.ReadAllBytesAsync(filePath);

            DumpSampleDataAndInfo(buffer, out var sampleData, out var info);

            var audioHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
            BassUtils.ReportError(nameof(Bass.CreateStream));
            var fxAudioHandle = BassFx.TempoCreate(audioHandle, BassFlags.FxFreeSource);
            BassUtils.ReportError(nameof(BassFx.TempoCreate));

            return new BassMusicPlayer(fxAudioHandle, audioLatency, sampleData, info);
        }

        public async Task<ISoundPlayer> LoadSoundAsync(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var soundHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);
            BassUtils.ReportError(nameof(Bass.CreateStream));

            var sample = Bass.ChannelGetAttribute(soundHandle, ChannelAttribute.Frequency);
            if (sample != OUTPUT_SAMPLES)
            {
                Log.LogWarn($"Sound file {filePath} samples {sample} != 48000");
                //just warn
            }

            return new BassSoundPlayer(soundHandle, soundMixer);
        }
    }
}
