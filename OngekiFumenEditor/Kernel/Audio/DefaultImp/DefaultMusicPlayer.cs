using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    internal class DefaultMusicPlayer : PropertyChangedBase, IAudioPlayer, ISchedulable
    {
        private AudioFileReader audioFileReader;
        private WaveOut currentOut;
        private TimeSpan baseOffset = TimeSpan.FromMilliseconds(0);
        private DateTime startTime;
        private TimeSpan pauseTime;

        public TimeSpan Duration { get => audioFileReader.TotalTime; }

        public TimeSpan CurrentTime { get => GetTime(); }

        public float PlaybackSpeed { get => 1; set { } }

        public bool IsPlaying { get => currentOut?.PlaybackState == PlaybackState.Playing; }

        public float Volume { get => currentOut?.Volume ?? 0; set => currentOut.Volume = value; }

        public string SchedulerName => $"DefaultMusicPlayer Playing Updater";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMilliseconds(1000.0 / 60);

        public Task Load(string audio_file)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                currentOut = new WaveOut();
                audioFileReader = new AudioFileReader(audio_file);
                currentOut?.Init(audioFileReader);
                NotifyOfPropertyChange(() => Duration);
            }
            catch (Exception e)
            {
                Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
                Dispose();
            }

            return Task.CompletedTask;
        }

        public void Seek(TimeSpan time, bool pause)
        {
            time = MathUtils.Max(TimeSpan.FromMilliseconds(0), MathUtils.Min(time, Duration));

            currentOut?.Stop();
            currentOut?.Dispose();
            currentOut = default;

            audioFileReader.Seek(0, System.IO.SeekOrigin.Begin);
            var provider = new OffsetSampleProvider(audioFileReader)
            {
                SkipOver = time
            };

            baseOffset = time;
            startTime = DateTime.Now;

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

        public void Dispose()
        {
            CleanCurrentOut();

            audioFileReader?.Dispose();
            audioFileReader = null;
        }

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            try
            {
                UpdatePropsManually();
            }
            catch
            {

            }
            return Task.CompletedTask;
        }

        private void UpdatePropsManually()
        {
            NotifyOfPropertyChange(() => CurrentTime);
            NotifyOfPropertyChange(() => Volume);
            NotifyOfPropertyChange(() => PlaybackSpeed);
            NotifyOfPropertyChange(() => IsPlaying);
        }
    }
}
