using System;

namespace OngekiFumenEditor.Utils
{
    public static class StringExtensionMethod
    {
        public static int ToInt(this string str, int defaultVal = default) => int.TryParse(str, out var val) ? val : defaultVal;
        public static float ToFloat(this string str, float defaultVal = default) => float.TryParse(str, out var val) ? val : defaultVal;
        public static long ToLong(this string str, long defaultVal = default) => long.TryParse(str, out var val) ? val : defaultVal;
        public static double ToDouble(this string str, double defaultVal = default) => double.TryParse(str, out var val) ? val : defaultVal;
        public static byte ToByte(this string str, byte defaultVal = default) => byte.TryParse(str, out var val) ? val : defaultVal;
        public static T ToEnum<T>(this string str, bool ignoreCase = true, T defaultVal = default) where T : struct, Enum => Enum.TryParse<T>(str, ignoreCase, out var val) ? val : defaultVal;

        public static int ToIntOrThrow(this string str) => int.TryParse(str, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to int");
        public static float ToFloatOrThrow(this string str) => float.TryParse(str, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to float");
        public static long ToLongOrThrow(this string str) => long.TryParse(str, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to long");
        public static double ToDoubleOrThrow(this string str) => double.TryParse(str, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to double");
        public static byte ToByteOrThrow(this string str) => byte.TryParse(str, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to byte");
        public static T ToEnumOrThrow<T>(this string str, bool ignoreCase = true) where T : struct, Enum => Enum.TryParse<T>(str, ignoreCase, out var val) ? val : throw new ArgumentException($"can't parse str \"{str}\" to enum {typeof(T).FullName}");

        public static string TrimEnd(this string str, string subStr) => str.EndsWith(subStr) ? str.Substring(0, str.Length - subStr.Length) : str;
        public static string TrimStart(this string str, string subStr) => str.StartsWith(subStr) ? str.Substring(subStr.Length) : str;

        public static string Format(this string str, params object[] args) => string.Format(str, args);
    }
}
