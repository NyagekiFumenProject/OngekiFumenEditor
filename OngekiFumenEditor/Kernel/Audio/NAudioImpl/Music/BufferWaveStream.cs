using NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music
{
    internal class BufferWaveStream : WaveStream, IWaveProvider, ISampleProvider
    {
        private readonly byte[] waveBuffer;
        private readonly WaveFormat format;

        public BufferWaveStream(byte[] buffer, WaveFormat format)
        {
            waveBuffer = buffer;
            this.format = format;
        }

        public override WaveFormat WaveFormat => format;

        public override long Length => waveBuffer.LongLength;

        public override long Position { get; set; } = 0;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = (int)Math.Min((long)count, waveBuffer.Length - Position);
            if (available <= 0)
                return 0;

            Buffer.BlockCopy(waveBuffer, (int)Position, buffer, offset, available);
            Position += available;
            return available;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var floatBuffer = MemoryMarshal.Cast<byte, float>(waveBuffer);
            var floatPosition = (int)(Position / sizeof(float));
            var available = Math.Min(count, floatBuffer.Length - floatPosition);
            if (available <= 0)
                return 0;

            floatBuffer.Slice(floatPosition, available).CopyTo(buffer.AsSpan(offset, available));
            Position = (floatPosition + available) * sizeof(float);
            return available;
        }
    }
}
