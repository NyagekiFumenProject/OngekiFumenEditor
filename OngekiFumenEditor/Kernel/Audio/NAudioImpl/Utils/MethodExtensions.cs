using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VGAudio.Formats;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils
{
	internal static class MethodExtensions
	{
		public static float[] ToArray(this ISampleProvider sampleProvider)
		{
			var buffer = ArrayPool<float>.Shared.Rent(1024_000);
			var list = new List<(float[], int)>();

			while (true)
			{
				var read = sampleProvider.Read(buffer, 0, buffer.Length);
				if (read == 0)
					break;

				var b = ArrayPool<float>.Shared.Rent(read);
				buffer.AsSpan()[..read].CopyTo(b);
				list.Add((b, read));
			}

			var totalLength = list.Select(x => x.Item2).Sum();
			var newBuf = new float[totalLength];

			var r = 0;
			foreach ((var b, var read) in list)
			{
				b.AsSpan()[..read].CopyTo(newBuf.AsSpan().Slice(r, read));
				r += read;
				ArrayPool<float>.Shared.Return(b);
			}
			ArrayPool<float>.Shared.Return(buffer);

			return newBuf;
		}
		public static byte[] ToArray(this IWaveProvider waveProvider)
		{
			var buffer = ArrayPool<byte>.Shared.Rent(1024_000);
			var list = new List<(byte[], int)>();

			while (true)
			{
				var read = waveProvider.Read(buffer, 0, buffer.Length);
				if (read == 0)
					break;

				var b = ArrayPool<byte>.Shared.Rent(read);
				buffer.AsSpan()[..read].CopyTo(b);
				list.Add((b, read));
			}

			var totalLength = list.Select(x => x.Item2).Sum();
			var newBuf = new byte[totalLength];

			var r = 0;
			foreach ((var b, var read) in list)
			{
				b.AsSpan()[..read].CopyTo(newBuf.AsSpan().Slice(r, read));
				r += read;
				ArrayPool<byte>.Shared.Return(b);
			}
			ArrayPool<byte>.Shared.Return(buffer);

			return newBuf;
		}

		public static async Task CopyToAsync(this IWaveProvider waveProvider, Stream stream)
		{
			var buffer = ArrayPool<byte>.Shared.Rent(1024_000);
			int read;
			while ((read = waveProvider.Read(buffer, 0, buffer.Length)) > 0)
				await stream.WriteAsync(buffer, 0, read);
		}

		public static Task CopyToAsync(this ISampleProvider waveProvider, Stream stream)
		{
			return waveProvider.ToWaveProvider().CopyToAsync(stream);
		}
	}
}
