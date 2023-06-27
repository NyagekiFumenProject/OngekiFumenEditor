using Gemini.Framework.Services;
using ManagedBass;
using ManagedBass.Mix;
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
    internal class BassManager : IAudioManager
    {
        public float SoundVolume { get => 1; set {
            //todo
            } }

        //private int masterMixer = 0;
        private int soundMixer = 0;

        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
            (".mp3","音频文件"),
            (".wav","音频文件"),
            //(".acb","Criware音频文件"),
        };

        public BassManager()
        {
            WindowInteropHelper helper = new WindowInteropHelper(App.Current.MainWindow);
            var handle = helper.Handle;

            Bass.Init(-1, 48000, DeviceInitFlags.Default, handle, default);
            BassUtils.ReportError(nameof(Bass.Init));

            Bass.GetInfo(out var info);
            BassUtils.ReportError(nameof(Bass.GetInfo));

            var deviceLatencyMS = info.Latency;

            soundMixer = BassMix.CreateMixerStream(48000, 2, BassFlags.MixerNonStop);
            BassUtils.ReportError(nameof(BassMix.CreateMixerStream));

            Bass.ChannelPlay(soundMixer);
            BassUtils.ReportError(nameof(Bass.ChannelPlay));
        }

        public void Dispose()
        {
            if (soundMixer != 0)
                Bass.StreamFree(soundMixer);
            soundMixer = 0;

            /*
            if (Bass.Free())
                BassUtils.ReportError(nameof(Bass.Free));
            */
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var audioHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);
            BassUtils.ReportError(nameof(Bass.CreateStream));

            if (audioHandle == 0)
                throw new Exception($"Bass can't read audio file: {filePath} , error: {Bass.LastError}");

            return new BassMusicPlayer(audioHandle);
        }

        public async Task<ISoundPlayer> LoadSoundAsync(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var soundHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);
            BassUtils.ReportError(nameof(Bass.CreateStream));

            var sample = Bass.ChannelGetAttribute(soundHandle, ChannelAttribute.Frequency);
            if (sample != 48000)
            {
                //just warn
            }

            return new BassSoundPlayer(soundHandle, soundMixer);

        }
    }
}
