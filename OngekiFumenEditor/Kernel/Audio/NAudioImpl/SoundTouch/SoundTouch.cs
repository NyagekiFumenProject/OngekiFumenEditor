using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.SoundTouch
{
    class SoundTouch : IDisposable
    {
        private IntPtr handle;
        private string versionString;
        public SoundTouch()
        {
            handle = SoundTouchInterop64.soundtouch_createInstance();
        }

        public string VersionString
        {
            get
            {
                if (versionString == null)
                {
                    var s = new StringBuilder(100);
                    SoundTouchInterop64.soundtouch_getVersionString2(s, s.Capacity);
                    versionString = s.ToString();
                }
                return versionString;
            }
        }

        public void SetPitchOctaves(float pitchOctaves)
        {
            SoundTouchInterop64.soundtouch_setPitchOctaves(handle, pitchOctaves);
        }

        public void SetSampleRate(int sampleRate)
        {
            SoundTouchInterop64.soundtouch_setSampleRate(handle, (uint)sampleRate);
        }

        public void SetChannels(int channels)
        {
            SoundTouchInterop64.soundtouch_setChannels(handle, (uint)channels);
        }

        private void DestroyInstance()
        {
            if (handle != IntPtr.Zero)
            {
                SoundTouchInterop64.soundtouch_destroyInstance(handle);
                handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            DestroyInstance();
            GC.SuppressFinalize(this);
        }

        ~SoundTouch()
        {
            DestroyInstance();
        }

        public void PutSamples(float[] samples, int numSamples)
        {
            SoundTouchInterop64.soundtouch_putSamples(handle, samples, numSamples);
        }

        public int ReceiveSamples(float[] outBuffer, int maxSamples)
        {
            return (int)SoundTouchInterop64.soundtouch_receiveSamples(handle, outBuffer, (uint)maxSamples);
        }

        public bool IsEmpty
        {
            get
            {
                return SoundTouchInterop64.soundtouch_isEmpty(handle) != 0;
            }
        }

        public int NumberOfSamplesAvailable
        {
            get
            {
                return (int)SoundTouchInterop64.soundtouch_numSamples(handle);
            }
        }

        public int NumberOfUnprocessedSamples
        {
            get
            {
                return SoundTouchInterop64.soundtouch_numUnprocessedSamples(handle);
            }
        }

        public void Flush()
        {
            SoundTouchInterop64.soundtouch_flush(handle);
        }

        public void Clear()
        {
            SoundTouchInterop64.soundtouch_clear(handle);
        }

        public void SetRate(float newRate)
        {
            SoundTouchInterop64.soundtouch_setRate(handle, newRate);
        }

        public void SetTempo(float newTempo)
        {
            SoundTouchInterop64.soundtouch_setTempo(handle, newTempo);
        }

        public int GetUseAntiAliasing()
        {
            return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.UseAaFilter);
        }

        public void SetUseAntiAliasing(bool useAntiAliasing)
        {
            SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.UseAaFilter, useAntiAliasing ? 1 : 0);
        }

        public void SetUseQuickSeek(bool useQuickSeek)
        {
            SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.UseQuickSeek, useQuickSeek ? 1 : 0);
        }

        public int GetUseQuickSeek()
        {
            return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.UseQuickSeek);
        }
    }
}
