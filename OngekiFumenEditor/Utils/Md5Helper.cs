using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OngekiFumenEditor.Utils
{
	internal static class Md5Helper
	{
		private static MD5 md5 = MD5.Create();

		public static string CalculateStringHash(string input)
		{
			var bytes = Encoding.UTF8.GetBytes(input);
			return string.Concat(md5.ComputeHash(bytes).Select(x => $"{x:x2}"));
		}
	}
}
