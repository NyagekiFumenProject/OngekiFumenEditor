using Caliburn.Micro;
using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Audio.HCA;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IAudioManager))]
    public class DefaultAudioManager : IAudioManager
    {
        private HashSet<IAudioPlayer> ownAudioPlayers = new();

        private readonly IWavePlayer soundOutputDevice;
        private readonly MixingSampleProvider soundMixer;
        private readonly VolumeSampleProvider soundVolumeWrapper;

        public float SoundVolume { get => soundVolumeWrapper.Volume; set => soundVolumeWrapper.Volume = value; }

        public DefaultAudioManager()
        {
            soundOutputDevice = new WasapiOut();
            soundMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            soundMixer.ReadFully = true;
            soundVolumeWrapper = new VolumeSampleProvider(soundMixer);
            soundOutputDevice.Init(soundVolumeWrapper);
            soundOutputDevice.Play();
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == soundMixer.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && soundMixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }
            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        public void PlaySound(CachedSound sound)
        {
            AddMixerInput(new CachedSoundSampleProvider(sound));
        }

        public void AddMixerInput(ISampleProvider input)
        {
            soundMixer.AddMixerInput(ConvertToRightChannelCount(input));
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            if (filePath.EndsWith(".acb"))
            {
                filePath = await ParseAndDecodeACBFile(filePath);
                if (filePath is null)
                    return null;
            }

            var player = new DefaultMusicPlayer();
            ownAudioPlayers.Add(player);

            await player.Load(filePath);
            return player;
        }

        private async Task<string> ParseAndDecodeACBFile(string filePath)
        {
            async ValueTask ProcessAllBinaries(uint acbFormatVersion, string extractFilePath, Afs2Archive archive, Stream dataStream)
            {
                async ValueTask DecodeHca(Stream hcaDataStream, Stream waveStream, DecodeParams decodeParams)
                {
                    using var hcaStream = new OneWayHcaAudioStream(hcaDataStream, decodeParams, true);
                    var buffer = ArrayPool<byte>.Shared.Rent(10240);
                    var read = 1;

                    while (read > 0)
                    {
                        read = await hcaStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            await waveStream.WriteAsync(buffer, 0, read);
                        }
                    }
                    ArrayPool<byte>.Shared.Return(buffer);
                }


                foreach (var entry in archive.Files)
                {
                    var record = entry.Value;
                    var len = (int)record.FileLength;
                    var buffer = ArrayPool<byte>.Shared.Rent(len);
                    dataStream.Seek(record.FileOffsetAligned, SeekOrigin.Begin);
                    var read = await dataStream.ReadAsync(buffer, 0, len);
                    var fileData = new MemoryStream(buffer, 0, read);

                    if (HcaReader.IsHcaStream(fileData))
                    {
                        Log.LogDebug(string.Format("Processing {0} AFS: #{1} (offset={2} size={3})...   ", acbFormatVersion, record.CueId, record.FileOffsetAligned, record.FileLength));

                        try
                        {
                            using var fs = File.Open(extractFilePath, FileMode.Create, FileAccess.Write, FileShare.Write);
                            await DecodeHca(fileData, fs, DecodeParams.Default);

                            Log.LogDebug("decoded");
                        }
                        catch (Exception ex)
                        {
                            if (File.Exists(extractFilePath))
                                File.Delete(extractFilePath);

                            Log.LogDebug(ex.ToString());

                            if (ex.InnerException != null)
                            {
                                Log.LogDebug("Details:");
                                Log.LogDebug(ex.InnerException.ToString());
                            }
                        }
                    }
                    else
                    {
                        Log.LogDebug("skipped (not HCA)");
                    }
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            Log.LogInfo($"Extract .acb to .wav and load the later , acb file path : {filePath}");

            using (var acb = AcbFile.FromFile(filePath))
            {
                var formatVersion = acb.FormatVersion;
                var awb = acb.InternalAwb ?? acb.ExternalAwb;
                var tempAwbFilePath = Path.GetTempFileName() + ".wav";

                try
                {
                    using var awbStream = awb == acb.InternalAwb ? acb.Stream : File.OpenRead(awb.FileName);
                    await ProcessAllBinaries(acb.FormatVersion, tempAwbFilePath, awb, awbStream);
                    return tempAwbFilePath;
                }
                catch (Exception e)
                {
                    Log.LogError($"Load acb file failed : {e.Message}");
                    return null;
                }
            }
        }


        public Task<ISoundPlayer> LoadSoundAsync(string filePath)
        {
            var cached = new CachedSound(filePath);
            return Task.FromResult<ISoundPlayer>(new DefaultSoundPlayer(cached, this));
        }

        public void Dispose()
        {
            Log.LogDebug("call DefaultAudioManager.Dispose()");
            foreach (var player in ownAudioPlayers)
                player?.Dispose();
            ownAudioPlayers.Clear();
            soundOutputDevice?.Dispose();
        }
    }
}
