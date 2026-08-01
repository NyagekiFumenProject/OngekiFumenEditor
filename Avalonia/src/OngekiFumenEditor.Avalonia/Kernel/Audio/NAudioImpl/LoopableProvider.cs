using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl
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

		public int Read(Span<float> buffer)
		{
			var totalRead = 0;
			do
			{
				var read = ReadInternal(buffer[totalRead..]);
				if (read <= 0)
					break;

				totalRead += read;
			}
			while (MakeSureBufferWriten && totalRead < buffer.Length);

			return totalRead;
		}

		private int ReadInternal(Span<float> buffer)
		{
			if (source is null)
				return 0;

			if (!isCached)
			{
				var read = source.Read(buffer);
				if (read <= 0)
				{
					isCached = true;
					bufferStream.Position = 0;
					readBuffer = bufferStream.ToArray();
					position = 0;
					bufferStream = null;
					return readBuffer.Length == 0 ? 0 : Read(buffer);
				}
				else
				{
					var byteSpan = MemoryMarshal.AsBytes(buffer[..read]);
					bufferStream.Write(byteSpan);
				}
				return read;
			}
			else
			{
				if (readBuffer.Length == 0)
					return 0;

				var read = 0;
				var refByteBuf = MemoryMarshal.AsBytes(buffer);
				foreach (ref var p in refByteBuf)
				{
					p = readBuffer[position++];
					position = position % readBuffer.Length;
					read++;
				}
				return buffer.Length;
			}
		}
	}
}


