using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.SoundTouch;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music
{
    /// <summary>
    /// 基于输出设备实际播放位置（<see cref="NAudio.Wave.IWavePosition"/>）的播放器实现。
    /// 不使用 Stopwatch，也没有旧实现的回退路径；仅支持 WasapiOut / WaveOut / WaveOutEvent 等实现 IWavePosition 的后端。
    /// 目前尚未接入 NAudioManager，保留旧 DefaultMusicPlayer 作为生产实现。
    /// </summary>
    internal class DefaultMusicPlayer_new : PropertyChangedBase, IAudioPlayer, ISchedulable
    {
        private FinishedListenerProvider finishProvider;

        private readonly object lifecycleSync = new();
        private long lifecycleVersion;

        private TimeSpan baseOffset = TimeSpan.FromMilliseconds(0);
        private TimeSpan pauseTime;
        private WaveFormat outputFormat;
        private long devicePositionBase;
        private long lastRawDevicePosition;
        private long deviceWrapOffset;
        private bool isAvaliable;
        private byte[] samples;
        private BufferWaveStream audioFileReader;

        private readonly MixingSampleProvider musicMixer;
        private readonly NAudioManager manager;

        public event IAudioPlayer.OnPlaybackFinishedFunc OnPlaybackFinished;

        public TimeSpan Duration
        {
            get
            {
                lock (lifecycleSync)
                    return duration;
            }
        }

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

        public DefaultMusicPlayer_new(MixingSampleProvider soundMixer, NAudioManager manager)
        {
            this.musicMixer = soundMixer;
            this.manager = manager;
        }

        private void Provider_OnReturnEmptySamples(FinishedListenerProvider source)
        {
            IAudioPlayer.OnPlaybackFinishedFunc callback;
            lock (lifecycleSync)
            {
                // A mixer read can finish after the provider has been removed. Ignore
                // callbacks from that stale provider instead of touching a newly loaded
                // reader (or a disposed one).
                if (!ReferenceEquals(source, finishProvider) || !IsAvaliable)
                    return;

                source.StopListen();
                callback = OnPlaybackFinished;
            }

            callback?.Invoke();
        }

        public async Task Load(string audio_file, int targetSampleRate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Release the previous resource before loading a new one. Dispose invalidates
            // an in-flight load, so a concurrent close/reload cannot publish stale state.
            Dispose();

            long loadVersion;
            lock (lifecycleSync)
                loadVersion = ++lifecycleVersion;

            try
            {
                Log.LogInfo($"Load audio file: {audio_file}");
                using var processedProvider = await AudioCompatibilizer.OpenSampleProvider(audio_file, targetSampleRate, cancellationToken);
                duration = processedProvider.Duration;

                cancellationToken.ThrowIfCancellationRequested();

                var loadedSamples = processedProvider.ToWaveProvider().ToArray();
                var loadedReader = new BufferWaveStream(loadedSamples, processedProvider.WaveFormat);
                loadedReader.Seek(0, SeekOrigin.Begin);
                var loadedFinishProvider = new FinishedListenerProvider(loadedReader);
                loadedFinishProvider.StartListen();

                lock (lifecycleSync)
                {
                    if (loadVersion != lifecycleVersion)
                    {
                        loadedReader.Dispose();
                        return;
                    }

                    samples = loadedSamples;
                    audioFileReader = loadedReader;
                    finishProvider = loadedFinishProvider;
                    finishProvider.OnReturnEmptySamples += Provider_OnReturnEmptySamples;
                    duration = loadedReader.TotalTime;
                    IsAvaliable = true;
                }

                NotifyOfPropertyChange(() => Duration);
            }
            catch (OperationCanceledException)
            {
                Dispose();
                throw;
            }
            catch (Exception e)
            {
                Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
                Dispose();
            }
        }

        public void Seek(TimeSpan seekTime, bool pause)
        {
            lock (lifecycleSync)
            {
                if (!IsAvaliable || audioFileReader is null || finishProvider is null)
                    return;

                seekTime = MathUtils.Max(TimeSpan.Zero, MathUtils.Min(seekTime, duration));
                var bytePosition = (long)(audioFileReader.WaveFormat.AverageBytesPerSecond * seekTime.TotalSeconds);
                // 对齐到完整样本帧，避免从半帧/半样本开始读取导致左右声道错位。
                var blockAlign = audioFileReader.WaveFormat.BlockAlign;
                if (blockAlign > 0)
                    bytePosition -= bytePosition % blockAlign;
                audioFileReader.Seek(bytePosition, SeekOrigin.Begin);
                // More accurate.
                baseOffset = audioFileReader.CurrentTime;
                pauseTime = baseOffset;

                if (IsPlaying)
                {
                    if (!manager.TryGetOutputPosition(out var rawPosition, out var format))
                        throw new NotSupportedException("当前音频输出设备不支持 IWavePosition，无法提供设备播放位置。");

                    outputFormat = format;
                    lastRawDevicePosition = rawPosition;
                    deviceWrapOffset = 0;
                    devicePositionBase = rawPosition;
                }

                finishProvider.StartListen();
            }

            if (!pause)
                Play();
            UpdatePropsManually();
        }

        public void Play()
        {
            lock (lifecycleSync)
            {
                if (!IsAvaliable || IsPlaying || finishProvider is null)
                    return;

                if (!manager.TryGetOutputPosition(out var rawPosition, out var format))
                    throw new NotSupportedException("当前音频输出设备不支持 IWavePosition，无法提供设备播放位置。");

                outputFormat = format;
                lastRawDevicePosition = rawPosition;
                deviceWrapOffset = 0;
                devicePositionBase = rawPosition;
                IsPlaying = true;
                musicMixer.AddMixerInput(finishProvider);
            }

            UpdatePropsManually();
            manager.Reposition();
            _ = IoC.Get<ISchedulerManager>().AddScheduler(this);
        }

        private TimeSpan GetTime()
        {
            lock (lifecycleSync)
            {
                if (!IsPlaying)
                    return pauseTime;

                var elapsedBytes = GetExpandedOutputPosition() - devicePositionBase;
                if (elapsedBytes < 0)
                    elapsedBytes = 0;

                var elapsedSeconds = elapsedBytes / (double)outputFormat.AverageBytesPerSecond;
                var adjustedTime = baseOffset + TimeSpan.FromSeconds(elapsedSeconds * manager.MusicSpeed);
                return MathUtils.Max(TimeSpan.Zero, adjustedTime);
            }
        }

        private long GetExpandedOutputPosition()
        {
            if (!manager.TryGetOutputPosition(out var rawPosition, out var format))
                throw new NotSupportedException("当前音频输出设备不支持 IWavePosition，无法提供设备播放位置。");

            outputFormat = format;
            // WaveOut 的位置是 32 位计数器；回绕时补上 2^32，64 位后端不会触发。
            if (rawPosition < lastRawDevicePosition)
                deviceWrapOffset += 1L << 32;

            lastRawDevicePosition = rawPosition;
            return rawPosition + deviceWrapOffset;
        }

        public void Stop()
        {
            lock (lifecycleSync)
            {
                if (!IsAvaliable || audioFileReader is null || finishProvider is null)
                    return;

                IsPlaying = false;
                musicMixer.RemoveMixerInput(finishProvider);
                audioFileReader.Seek(0, SeekOrigin.Begin);
                baseOffset = audioFileReader.CurrentTime;
                pauseTime = baseOffset;
                finishProvider.StartListen();
            }

            _ = IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            UpdatePropsManually();
        }

        public void Pause()
        {
            lock (lifecycleSync)
            {
                if (!IsAvaliable || !IsPlaying || finishProvider is null)
                    return;

                pauseTime = GetTime();
                baseOffset = pauseTime;
                IsPlaying = false;
                musicMixer.RemoveMixerInput(finishProvider);
            }

            UpdatePropsManually();
            _ = IoC.Get<ISchedulerManager>().RemoveScheduler(this);
        }

        private void CleanCurrentOut()
        {
            if (finishProvider is not null)
                musicMixer.RemoveMixerInput(finishProvider);
            UpdatePropsManually();
        }

        public void Dispose()
        {
            BufferWaveStream readerToDispose;
            FinishedListenerProvider providerToRemove;
            lock (lifecycleSync)
            {
                lifecycleVersion++;
                providerToRemove = finishProvider;
                readerToDispose = audioFileReader;

                if (providerToRemove is not null)
                {
                    providerToRemove.StopListen();
                    providerToRemove.OnReturnEmptySamples -= Provider_OnReturnEmptySamples;
                    musicMixer.RemoveMixerInput(providerToRemove);
                }

                finishProvider = null;
                audioFileReader = null;
                samples = null;
                duration = TimeSpan.Zero;
                pauseTime = TimeSpan.Zero;
                baseOffset = TimeSpan.Zero;
                outputFormat = null;
                devicePositionBase = 0;
                lastRawDevicePosition = 0;
                deviceWrapOffset = 0;
                IsAvaliable = false;
                IsPlaying = false;
            }

            readerToDispose?.Dispose();
            _ = IoC.Get<ISchedulerManager>().RemoveScheduler(this);
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
            lock (lifecycleSync)
            {
                if (!IsAvaliable || samples is null || audioFileReader is null)
                    return Task.FromResult<SampleData>(default);

                var subBuffer = samples.AsMemory();
                var sampleData = new SampleData(subBuffer, ConvertToSampleInfo(audioFileReader.WaveFormat));
                return Task.FromResult(sampleData);
            }
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
