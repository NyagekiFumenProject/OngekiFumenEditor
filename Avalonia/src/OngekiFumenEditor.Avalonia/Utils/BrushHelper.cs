using Avalonia.Media;

namespace OngekiFumenEditor.Avalonia.Utils
{
	public static class BrushHelper
	{
		public static SolidColorBrush CreateSolidColorBrush(Color color)
		{
			var brush = new SolidColorBrush(color);
			return brush;
		}
	}
}
