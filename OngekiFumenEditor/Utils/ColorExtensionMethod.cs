using System;
using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
	public static class ColorExtensionMethod
	{
		public static float ColorDistance(this Color a, Color b)
		{
			byte ra = a.R, rb = b.R, ga = a.G, gb = b.G, ba = a.B, bb = b.B;
			var rm = (ra + rb) / 2.0f;
			var R = (ra - rb);
			var G = (ga - gb);
			var B = (ba - bb);
			return MathF.Sqrt((2 + rm / 256.0f) * MathF.Pow(R, 2) + 4 * MathF.Pow(G, 2) + (2 + (255 - rm) / 256.0f) * MathF.Pow(B, 2));
		}
	}
}