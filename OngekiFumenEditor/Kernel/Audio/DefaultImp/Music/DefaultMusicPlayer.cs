using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Music
{
    internal class DefaultMusicPlayer : PropertyChangedBase, IAudioPlayer, ISchedulable
    {
        private AudioFileReader audioFileReader;
        private VolumeSampleProvider currentVolumeProvider;
        private WaveOut currentOut;
        private TimeSpan baseOffset = TimeSpan.FromMilliseconds(0);
        private DateTime startTime;
        private TimeSpan pauseTime;
        private float volume = 1;
        private bool isAvaliable;
        private byte[] samples;

        public TimeSpan Duration { get => audioFileReader.TotalTime; }

        public TimeSpan CurrentTime { get => GetTime(); }

        public float PlaybackSpeed { get => 1; set { } }

        public bool IsPlaying { get => currentOut?.PlaybackState == PlaybackState.Playing; }

        public float Volume { 
            get => volume;
            set
            {
                volume = value;
                if (currentVolumeProvider is not null)
                    currentVolumeProvider.Volume = volume;
            }
        }

        public string SchedulerName => $"DefaultMusicPlayer Playing Updater";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMilliseconds(1000.0 / 60);

        public bool IsAvaliable
        {
            get => isAvaliable;
            set
            {
                Set(ref isAvaliable, value);
            }
        }

        public async Task Load(string audio_file)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                currentOut = new WaveOut();
                audioFileReader = new AudioFileReader(audio_file);
                var ms = new MemoryStream();
                await audioFileReader.CopyToAsync(ms);
                audioFileReader.Seek(0, SeekOrigin.Begin);
                samples = ms.ToArray();
                currentOut?.Init(audioFileReader);
                NotifyOfPropertyChange(() => Duration);
                IsAvaliable = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
                Dispose();
            }
        }

        public void Seek(TimeSpan time, bool pause)
        {
            time = MathUtils.Max(TimeSpan.FromMilliseconds(0), MathUtils.Min(time, Duration));

            currentOut?.Stop();
            currentOut?.Dispose();
            currentOut = default;

            audioFileReader.Seek(0, SeekOrigin.Begin);
            var provider = new VolumeSampleProvider(new OffsetSampleProvider(audioFileReader)
            {
                SkipOver = time
            })
            {
                Volume = Volume
            };

            baseOffset = time;
            startTime = DateTime.Now;

            currentVolumeProvider = provider;
            currentOut = new WaveOut();
            currentOut.Init(provider);
            UpdatePropsManually();

            if (!pause)
                Play();
        }

        public async void Play()
        {
            currentOut?.Play();
            startTime = DateTime.Now;
            await IoC.Get<ISchedulerManager>().AddScheduler(this);
        }

        private TimeSpan GetTime()
        {
            if (!IsPlaying)
                return pauseTime;
            var coreTime = DateTime.Now - startTime;
            var actualTime = coreTime/* * currentOutPositionWeight*/ + baseOffset;
            return actualTime;
        }

        public async void Stop()
        {
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            Seek(TimeSpan.FromMilliseconds(0), true);
        }

        public async void Pause()
        {
            pauseTime = GetTime();
            currentOut?.Pause();
            UpdatePropsManually();
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
        }

        private void CleanCurrentOut()
        {
            currentOut?.Stop();
            currentOut?.Dispose();
            currentOut = null;
            UpdatePropsManually();
        }

        public async void Dispose()
        {
            CleanCurrentOut();

            audioFileReader?.Dispose();
            audioFileReader = null;
            IsAvaliable = false;

            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
        }

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
                UpdatePropsManually();
            return Task.CompletedTask;
        }

        private void UpdatePropsManually()
        {
            if (!IsAvaliable)
                return;

            NotifyOfPropertyChange(() => CurrentTime);
            NotifyOfPropertyChange(() => Volume);
            NotifyOfPropertyChange(() => PlaybackSpeed);
            NotifyOfPropertyChange(() => IsPlaying);
        }

        public Task<SampleData> GetSamplesAsync()
        {
            if (!IsAvaliable)
                return Task.FromResult<SampleData>(default);

            var subBuffer = samples.AsMemory();
            var sampleData = new SampleData(subBuffer, ConvertToSampleInfo(audioFileReader.WaveFormat));

            return Task.FromResult(sampleData);
        }

        public static SampleInfo ConvertToSampleInfo(WaveFormat waveFormat)
        {
            var sampleInfo = new SampleInfo();

            sampleInfo.SampleRate = waveFormat.SampleRate;
            sampleInfo.Channels = waveFormat.Channels;
            sampleInfo.BitsPerSample = waveFormat.BitsPerSample;

            return sampleInfo;
        }
    }
}
