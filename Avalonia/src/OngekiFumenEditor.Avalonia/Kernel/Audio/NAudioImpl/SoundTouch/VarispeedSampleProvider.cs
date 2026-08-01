using System;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.SoundTouch
{
    class VarispeedSampleProvider : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider sourceProvider;
        private readonly SoundTouch soundTouch;
        private readonly float[] sourceReadBuffer;
        private readonly float[] soundTouchReadBuffer;
        private readonly int channelCount;
        private float playbackRate = 1.0f;
        private SoundTouchProfile currentSoundTouchProfile;
        private bool repositionRequested;

        public VarispeedSampleProvider(ISampleProvider sourceProvider, int readDurationMilliseconds, SoundTouchProfile soundTouchProfile)
        {
            soundTouch = new SoundTouch();

            Log.LogDebug($"SoundTouch Version {soundTouch.VersionString}");
            Log.LogDebug($"Use QuickSeek: {soundTouch.GetUseQuickSeek()}");
            Log.LogDebug($"Use AntiAliasing: {soundTouch.GetUseAntiAliasing()}");

            SetSoundTouchProfile(soundTouchProfile);
            this.sourceProvider = sourceProvider;
            soundTouch.SetSampleRate(WaveFormat.SampleRate);
            channelCount = WaveFormat.Channels;
            soundTouch.SetChannels(channelCount);
            sourceReadBuffer = new float[(WaveFormat.SampleRate * channelCount * (long)readDurationMilliseconds) / 1000];
            soundTouchReadBuffer = new float[sourceReadBuffer.Length * 10]; // support down to 0.1 speed
        }

        public int Read(Span<float> buffer)
        {
            if (playbackRate == 0) // play silence
            {
                buffer.Clear();
                return buffer.Length;
            }

            if (repositionRequested)
            {
                soundTouch.Clear();
                repositionRequested = false;
            }

            int samplesRead = 0;
            bool reachedEndOfSource = false;
            while (samplesRead < buffer.Length)
            {
                if (soundTouch.NumberOfSamplesAvailable == 0)
                {
                    var readFromSource = sourceProvider.Read(sourceReadBuffer);
                    if (readFromSource > 0)
                    {
                        soundTouch.PutSamples(sourceReadBuffer, readFromSource / channelCount);
                    }
                    else
                    {
                        reachedEndOfSource = true;
                        // we've reached the end, tell SoundTouch we're done
                        soundTouch.Flush();
                    }
                }
                var desiredSampleFrames = (buffer.Length - samplesRead) / channelCount;

                var received = soundTouch.ReceiveSamples(soundTouchReadBuffer, desiredSampleFrames) * channelCount;
                soundTouchReadBuffer.AsSpan(0, received).CopyTo(buffer[samplesRead..]);
                samplesRead += received;
                if (received == 0 && reachedEndOfSource) break;
            }
            return samplesRead;
        }

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public float PlaybackRate
        {
            get
            {
                return playbackRate;
            }
            set
            {
                if (playbackRate != value)
                {
                    UpdatePlaybackRate(value);
                    playbackRate = value;
                }
            }
        }

        private void UpdatePlaybackRate(float value)
        {
            if (value != 0)
            {
                if (currentSoundTouchProfile.UseTempo)
                {
                    soundTouch.SetTempo(value);
                }
                else
                {
                    soundTouch.SetRate(value);
                }
            }
        }

        public void Dispose()
        {
            soundTouch.Dispose();
        }

        public void SetSoundTouchProfile(SoundTouchProfile soundTouchProfile)
        {
            if (currentSoundTouchProfile != null &&
                playbackRate != 1.0f &&
                soundTouchProfile.UseTempo != currentSoundTouchProfile.UseTempo)
            {
                if (soundTouchProfile.UseTempo)
                {
                    soundTouch.SetRate(1.0f);
                    soundTouch.SetPitchOctaves(0f);
                    soundTouch.SetTempo(playbackRate);
                }
                else
                {
                    soundTouch.SetTempo(1.0f);
                    soundTouch.SetRate(playbackRate);
                }
            }
            this.currentSoundTouchProfile = soundTouchProfile;
            soundTouch.SetUseAntiAliasing(soundTouchProfile.UseAntiAliasing);
            soundTouch.SetUseQuickSeek(soundTouchProfile.UseQuickSeek);
        }

        public void Reposition()
        {
            repositionRequested = true;
        }
    }
}

