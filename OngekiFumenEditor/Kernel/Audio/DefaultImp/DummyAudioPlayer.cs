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
        private float baseTime = 0;
        private Stopwatch stopwatch = new();

        public float CurrentTime => baseTime + stopwatch.ElapsedMilliseconds;

        private float volume;
        public float Volume
        {
            get => volume;
            set => Set(ref volume, value);
        }

        private float duration;
        public float Duration
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

        public Task Load(string audio_file)
        {
            //release resource before loading new one.
            Dispose();

            try
            {
                using var audioFileReader = new AudioFileReader(audio_file);
                Duration = (float)audioFileReader.TotalTime.TotalMilliseconds;
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

        public void Seek(float time, bool pause)
        {
            baseTime = time;
            stopwatch.Reset();
            if (pause)
                Pause();
        }

        public void Stop()
        {
            baseTime = 0;
            stopwatch.Reset();
            stopwatch.Stop();
        }
    }
}
