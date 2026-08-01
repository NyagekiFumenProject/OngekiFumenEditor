using NAudio.Wave;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Music
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
			=> Read(buffer.AsSpan(offset, count));

		public override int Read(Span<byte> buffer)
		{
			var available = waveBuffer.Length - (int)Position;
			var count = Math.Min(available, buffer.Length);
			waveBuffer.AsSpan((int)Position, count).CopyTo(buffer);
			Position += count;
			return count;
		}

		public int Read(Span<float> buffer)
		{
			var floatBuffer = MemoryMarshal.Cast<byte, float>(waveBuffer);

			var floatPosition = (int)(Position / sizeof(float));
			var floatLength = waveBuffer.Length / sizeof(float);

			var beforePosition = floatPosition;
			var count = Math.Min(buffer.Length, floatLength - floatPosition);
			floatBuffer.Slice(floatPosition, count).CopyTo(buffer);
			floatPosition += count;
			var read = floatPosition - beforePosition;
			Position = floatPosition * sizeof(float);
			return read;
		}
	}
}


