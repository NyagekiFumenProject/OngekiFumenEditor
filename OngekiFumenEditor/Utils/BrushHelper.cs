using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
	public static class BrushHelper
	{
		public static SolidColorBrush CreateSolidColorBrush(Color color)
		{
			var brush = new SolidColorBrush(color);
			brush.Freeze();
			return brush;
		}
	}
}
