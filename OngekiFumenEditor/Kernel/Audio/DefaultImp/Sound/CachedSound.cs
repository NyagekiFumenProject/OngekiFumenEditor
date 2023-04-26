using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Sound
{
    public class CachedSound
    {
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }

        public CachedSound(string audioFileName)
        {
            using var audioFileReader = new AudioFileReader(audioFileName);

            WaveFormat = audioFileReader.WaveFormat;
            var wholeFile = new List<float>((int)(audioFileReader.Length / 4));

            var readLen = audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels;
            var readBuffer = ArrayPool<float>.Shared.Rent(readLen);

            int samplesRead;
            while ((samplesRead = audioFileReader.Read(readBuffer, 0, readLen)) > 0)
            {
                wholeFile.AddRange(readBuffer.Take(samplesRead));
            }

            AudioData = wholeFile.ToArray();
            ArrayPool<float>.Shared.Return(readBuffer);
        }

        public CachedSound(float[] newBuf, WaveFormat outFormat)
        {
            AudioData = newBuf;
            WaveFormat = outFormat;
        }
    }
}
