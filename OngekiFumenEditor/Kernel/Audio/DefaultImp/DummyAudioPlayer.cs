using Caliburn.Micro;
using NAudio.Wave;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    public class DummyAudioPlayer : PropertyChangedBase, IAudioPlayer
    {
        private TimeSpan baseTime = TimeSpan.FromMilliseconds(0);
        private Stopwatch stopwatch = new();

        public TimeSpan CurrentTime => baseTime + TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds);

        private float volume;
        public float Volume
        {
            get => volume;
            set => Set(ref volume, value);
        }

        private TimeSpan duration;
        public TimeSpan Duration
        {
            get => duration;
            set => Set(ref duration, value);
        }

        private bool isPlaying;
        public bool IsPlaying
        {
            get => isPlaying;
            set => Set(ref isPlaying, value);
        }

        public bool IsAvaliable { get; } = true;

        public Task Load(string audio_file)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                using var audioFileReader = new AudioFileReader(audio_file);
                Duration = audioFileReader.TotalTime;
            }
            catch (Exception e)
            {
                Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
                Dispose();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Pause()
        {
            stopwatch.Stop();
            IsPlaying = false;
        }

        public void Play()
        {
            stopwatch.Start();
            IsPlaying = true;
        }

        public void Seek(TimeSpan time, bool pause)
        {
            baseTime = time;
            stopwatch.Reset();
            if (pause)
                Pause();
        }

        public void Stop()
        {
            baseTime = TimeSpan.FromMilliseconds(0);
            stopwatch.Reset();
            stopwatch.Stop();
        }
    }
}
