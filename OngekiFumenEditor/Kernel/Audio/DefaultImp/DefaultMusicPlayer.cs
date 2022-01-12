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

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    internal class DefaultMusicPlayer : PropertyChangedBase, IAudioPlayer, ISchedulable
    {
        private AudioFileReader audioFileReader;

        private WaveOutEvent currentOut;

        public float Duration { get => (float)audioFileReader.TotalTime.TotalMilliseconds; }

        public float CurrentTime { get => GetTime(); }

        public float PlaybackSpeed { get => 1; set { } }

        public bool IsPlaying { get => currentOut?.PlaybackState == PlaybackState.Playing; }

        public float Volume { get => currentOut.Volume; set => currentOut.Volume = value; }

        public string SchedulerName => $"DefaultMusicPlayer Playing Updater";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMilliseconds(1000.0 / 60);

        private float baseOffset = 0;

        public Task Load(string audio_file)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                currentOut = new WaveOutEvent();
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

        public void Jump(float time, bool pause)
        {
            time = Math.Max(0, Math.Min(time, Duration));

            currentOut?.Stop();

            audioFileReader.Seek(0, System.IO.SeekOrigin.Begin);
            var provider = new OffsetSampleProvider(audioFileReader)
            {
                SkipOver = TimeSpan.FromMilliseconds(time)
            };

            baseOffset = time;

            currentOut?.Init(provider);
            UpdatePropsManually();

            if (!pause)
                Play();
        }

        public async void Play()
        {
            currentOut?.Play();
            await IoC.Get<ISchedulerManager>().AddScheduler(this);
        }

        private float GetTime()
        {
            var time = (currentOut is null ? 0 : (currentOut.GetPosition() * 1000.0f / currentOut.OutputWaveFormat.BitsPerSample / currentOut.OutputWaveFormat.Channels * 8 / currentOut.OutputWaveFormat.SampleRate)) + baseOffset;

            return time;
        }

        public async void Stop()
        {
            await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
            Jump(0, true);
        }

        public async void Pause()
        {
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
            UpdatePropsManually();
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
