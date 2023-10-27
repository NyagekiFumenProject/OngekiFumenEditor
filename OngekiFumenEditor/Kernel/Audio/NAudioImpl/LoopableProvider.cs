using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl
{
	public class LoopableProvider : ISampleProvider, IDisposable
	{
		private ISampleProvider source;
		private MemoryStream bufferStream = new();
		private byte[] readBuffer;
		private int position;
		private bool isCached = false;

		public bool MakeSureBufferWriten { get; set; } = true;

		public LoopableProvider(ISampleProvider source)
		{
			this.source = source;
		}

		public WaveFormat WaveFormat => source.WaveFormat;

		public void Dispose()
		{
			readBuffer = default;
			source = default;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			var read = ReadInternal(buffer, offset, count);
			if (count != read && MakeSureBufferWriten)
			{
				var newCount = count - read;
				var newOffset = offset + read;

				return read + Read(buffer, newOffset, newCount);
			}
			return read;
		}

		private int ReadInternal(float[] buffer, int offset, int count)
		{
			if (source is null)
				return 0;

			if (!isCached)
			{
				var read = source.Read(buffer, offset, count);
				if (read <= 0)
				{
					isCached = true;
					bufferStream.Position = 0;
					readBuffer = bufferStream.ToArray();
					position = 0;
					bufferStream = null;
					return Read(buffer, offset, count);
				}
				else
				{
					var span = buffer.AsSpan(offset, read);
					var byteSpan = MemoryMarshal.AsBytes(span);
					bufferStream.Write(byteSpan);
				}
				return read;
			}
			else
			{
				var read = 0;
				var refByteBuf = MemoryMarshal.AsBytes(buffer.AsSpan(offset, count));
				foreach (ref var p in refByteBuf)
				{
					p = readBuffer[position++];
					position = position % readBuffer.Length;
					read++;
				}
				return count;
			}
		}
	}
}
