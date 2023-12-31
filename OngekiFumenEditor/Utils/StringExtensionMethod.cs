namespace OngekiFumenEditor.Utils
{
	public static class StringExtensionMethod
	{
		public static int ToInt(this string str, int defaultVal = default) => int.TryParse(str, out var val) ? val : defaultVal;
		public static float ToFloat(this string str, float defaultVal = default) => float.TryParse(str, out var val) ? val : defaultVal;
		public static long ToLong(this string str, long defaultVal = default) => long.TryParse(str, out var val) ? val : defaultVal;
		public static double ToDouble(this string str, double defaultVal = default) => double.TryParse(str, out var val) ? val : defaultVal;
		public static byte ToByte(this string str, byte defaultVal = default) => byte.TryParse(str, out var val) ? val : defaultVal;

		public static string TrimEnd(this string str, string subStr) => str.EndsWith(subStr) ? str.Substring(0, str.Length - subStr.Length) : str;
		public static string TrimStart(this string str, string subStr) => str.StartsWith(subStr) ? str.Substring(subStr.Length) : str;

		public static string Format(this string str, params object[] args) => string.Format(str, args);
	}
}
