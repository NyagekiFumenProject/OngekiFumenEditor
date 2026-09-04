using OngekiFumenEditor.Avalonia.Base;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl
{
	internal static partial class ParserUtils
	{
		public static Dictionary<string, string> GetValuesMap(this string paramsDataStr)
		{
			var map = new Dictionary<string, string>();
			foreach (var (name, value) in ParseParams(paramsDataStr))
				map[name] = value;

			return map;
		}

		[GeneratedRegex(@"(\w+)\[(.*?)\]\s*(,|$)")]
		private static partial Regex ParamRegex();

		public static IEnumerable<(string name, string value)> ParseParams(string content)
		{
			foreach (Match m in ParamRegex().Matches(content))
				yield return (m.Groups[1].Value, m.Groups[2].Value);
		}

		public static TGrid ParseToTGrid(this string tgridContent)
		{
			var data = tgridContent.Trim().Trim(new char[] { 'T', '[', ']' }).Split(",");
			return new TGrid(float.Parse(data[0]), int.Parse(data[1]));
		}

		public static XGrid ParseToXGrid(this string xgridContent)
		{
			var data = xgridContent.Trim(new char[] { 'X', '[', ']' }).Trim().Split(",");
			return new XGrid(float.Parse(data[0]), int.Parse(data[1]));
		}
	}
}


