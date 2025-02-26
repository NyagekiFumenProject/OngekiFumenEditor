﻿using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.SoundTouch;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Music
{
    internal class DefaultMusicPlayer : PropertyChangedBase, IAudioPlayer, ISchedulable
    {
        private FinishedListenerProvider finishProvider;

        private TimeSpan baseOffset = TimeSpan.FromMilliseconds(0);
        private Stopwatch sw = new();
        private TimeSpan pauseTime;
        private float volume = 1;
        private bool isAvaliable;
        private byte[] samples;
        private BufferWaveStream audioFileReader;

        private readonly MixingSampleProvider musicMixer;
        private readonly NAudioManager manager;

        public event IAudioPlayer.OnPlaybackFinishedFunc OnPlaybackFinished;

        public TimeSpan Duration => duration;

        public TimeSpan CurrentTime { get => GetTime(); }

        public float Speed { get => 1; set { } }

        private bool isPlaying;
        private TimeSpan duration;

        public bool IsPlaying
        {
            get => isPlaying;
            set => Set(ref isPlaying, value);
        }

        public float Volume
        {
            get => manager.MusicVolume;
            set
            {
                manager.MusicVolume = value;
                NotifyOfPropertyChange(() => Volume);
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

        public DefaultMusicPlayer(MixingSampleProvider soundMixer, NAudioManager manager)
        {
            this.musicMixer = soundMixer;
            this.manager = manager;
        }

        private void Provider_OnReturnEmptySamples()
        {
            finishProvider.StopListen();
            OnPlaybackFinished?.Invoke();
        }

        public async Task Load(string audio_file, int targetSampleRate)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                Log.LogInfo($"Load audio file: {audio_file}");
                var rawStream = new AudioFileReader(audio_file);
                duration = rawStream.TotalTime;
                var processedProvider = await AudioCompatibilizer.CheckCompatible(rawStream, targetSampleRate);

                samples = processedProvider.ToWaveProvider().ToArray();

                audioFileReader = new BufferWaveStream(samples, processedProvider.WaveFormat);
                audioFileReader.Seek(0, SeekOrigin.Begin);

                finishProvider = new(audioFileReader);
                finishProvider.StartListen();
                finishProvider.OnReturnEmptySamples += Provider_OnReturnEmptySamples;

                NotifyOfPropertyChange(() => Duration);
                IsAvaliable = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
                Dispose();
            }
        }

        public void Seek(TimeSpan seekTime, bool pause)
        {
            seekTime = MathUtils.Max(TimeSpan.FromMilliseconds(0), MathUtils.Min(seekTime, Duration));

            audioFileReader.Seek((long)(audioFileReader.WaveFormat.AverageBytesPerSecond * seekTime.TotalSeconds), SeekOrigin.Begin);
            //more accurate
            baseOffset = audioFileReader.CurrentTime;

            finishProvider.StartListen();

            if (!pause)
                Play();
            UpdatePropsManually();
        }

        public async void Play()
        {
            IsPlaying = true;
            sw.Restart();
            musicMixer.AddMixerInput(finishProvider);
            UpdatePropsManually();
            manager.Reposition();

            await IoC.Get<ISchedulerManager>().AddScheduler(this);
        }

        private TimeSpan GetTime()
        {
            if (!IsPlaying)
                return pauseTime;
            var offset = TimeSpan.FromTicks(sw.ElapsedTicks) * manager.MusicSpeed;
            var adjustedTime = offset + baseOffset - TimeSpan.FromMilliseconds(manager.SpeedCostDelayMs / 2);
            var actualTime = MathUtils.Max(TimeSpan.Zero, adjustedTime);
            return actualTime;
        }

        public async void Stop()
        {
            IsPlaying = false;
            musicMixer.RemoveMixerInput(finishProvider);
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            Seek(TimeSpan.FromMilliseconds(0), true);
            UpdatePropsManually();
        }

        public async void Pause()
        {
            pauseTime = GetTime();
            IsPlaying = false;
            musicMixer.RemoveMixerInput(finishProvider);
            UpdatePropsManually();
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
        }

        private void CleanCurrentOut()
        {
            musicMixer.RemoveMixerInput(finishProvider);
            UpdatePropsManually();
        }

        public async void Dispose()
        {
            CleanCurrentOut();

            audioFileReader?.Dispose();
            audioFileReader = null;
            IsAvaliable = false;
            IsPlaying = false;

            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
        }

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                UpdatePropsManually();
            }
            return Task.CompletedTask;
        }

        private void UpdatePropsManually()
        {
            if (!IsAvaliable)
                return;

            NotifyOfPropertyChange(() => CurrentTime);
            NotifyOfPropertyChange(() => Volume);
            NotifyOfPropertyChange(() => Speed);
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
