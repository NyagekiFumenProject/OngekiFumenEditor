using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace OngekiFumenEditor.Utils
{
    public static class RandomHepler
    {
        private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int max)
        {
            return System.Random.Shared.Next(max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int min, int max)
        {
            return System.Random.Shared.Next(min, max);
        }

        public static string RandomString(int length = 10)
        {
            var sb = new StringBuilder(length);
            var rand = System.Random.Shared;

            for (int i = 0; i < length; i++)
                sb.Append(CHARS[rand.Next(CHARS.Length)]);

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RandomDouble()
        {
            return System.Random.Shared.NextDouble();
        }
    }
}

