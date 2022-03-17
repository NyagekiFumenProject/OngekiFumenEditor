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
        private float baseOffset = 0;
        private DateTime startTime;
        private float pauseTime;

        public float Duration { get => (float)audioFileReader.TotalTime.TotalMilliseconds; }

        public float CurrentTime { get => GetTime(); }

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

        public void Seek(float time, bool pause)
        {
            time = Math.Max(0, Math.Min(time, Duration));

            currentOut?.Stop();
            currentOut?.Dispose();
            currentOut = default;

            audioFileReader.Seek(0, System.IO.SeekOrigin.Begin);
            var provider = new OffsetSampleProvider(audioFileReader)
            {
                SkipOver = TimeSpan.FromMilliseconds(time)
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

        private float GetTime()
        {
            if (!IsPlaying)
                return pauseTime;
            var coreTime = (float)(DateTime.Now - startTime).TotalMilliseconds;
            var actualTime = coreTime/* * currentOutPositionWeight*/ + baseOffset;
            return actualTime;
        }

        public async void Stop()
        {
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            Seek(0, true);
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
