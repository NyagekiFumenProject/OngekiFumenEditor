using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace OngekiFumenEditor.Utils
{
	public static class RandomHepler
	{
		const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

		private static Random rand = new Random($"ILoveOngeki_{DateTime.Now}".GetHashCode());
		private static StringBuilder sb = new StringBuilder();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Random(int max)
		{
			return rand.Next(max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Random(int min, int max)
		{
			return rand.Next(min, max);
		}

		public static string RandomString(int length = 10)
		{
			sb.Clear();

			for (int i = 0; i < length; i++)
				sb.Append(CHARS[rand.Next(CHARS.Length)]);

			return sb.ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static double RandomDouble()
		{
			return rand.NextDouble();
		}
	}
}
