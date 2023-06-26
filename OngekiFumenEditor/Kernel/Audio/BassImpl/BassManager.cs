using Gemini.Framework.Services;
using ManagedBass;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Music;
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
    //[Export(typeof(IAudioManager))]
    internal class BassManager : IAudioManager
    {
        public float SoundVolume { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
            //(".mp3","音频文件"),
            (".wav","音频文件"),
            //(".acb","Criware音频文件"),
        };

        public BassManager()
        {
            WindowInteropHelper helper = new WindowInteropHelper(App.Current.MainWindow);
            IntPtr handle = helper.Handle;

            if (!Bass.Init(-1, 48000, DeviceInitFlags.Default, handle, default))
                BassUtils.ReportError(nameof(Bass.Init));

            if (!Bass.GetInfo(out var info))
                BassUtils.ReportError(nameof(Bass.GetInfo));

            var deviceLatencyMS = info.Latency;
        }

        public void Dispose()
        {
            if (Bass.Free())
                BassUtils.ReportError(nameof(Bass.Free));
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var audioHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);

            if (audioHandle == 0)
                throw new Exception($"Bass can't read audio file: {filePath} , error: {Bass.LastError}");

            return new BassMusicPlayer(audioHandle);
        }

        public async Task<ISoundPlayer> LoadSoundAsync(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var audioHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);
        }
    }
}
