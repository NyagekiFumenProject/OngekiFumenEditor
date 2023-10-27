using System;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Parser
{
	public static class ParserUtils
	{
		public static readonly char[] SplitEmptyCharArray = new[] { ' ', '\t' };
		public static string[] SplitEmptyChar(string line) => line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries).ToArray();
		public static T[] GetDataArray<T>(string line)
		{
			var converter = TypeDescriptor.GetConverter(typeof(T));
			return SplitEmptyChar(line).Skip(1).Select(x =>
			{
				if (converter.IsValid(x))
					return (T)converter.ConvertFromString(x);
				return default;
			}).ToArray();
		}
	}
}
