using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl
{
	internal static class ParserUtils
	{
		public static IDisposable GetValuesMapWithDisposable(this string paramsDataStr, out Dictionary<string, string> map)
		{
			return ParseParams(paramsDataStr).ToDictionaryWithObjectPool(x => x.name, x => x.value, out map);
		}

		private static Regex s = new Regex(@"(\w+)\[(.*?)\]\s*(,|$)");

		public static IEnumerable<(string name, string value)> ParseParams(string content)
		{
			foreach (Match m in s.Matches(content))
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
