using OngekiFumenEditor.Utils;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
	public class BeatRhythmToBrushConverter : IValueConverter
	{
		public static readonly Brush DefaultRhythmBrush = Brushes.White;

		public static readonly Brush[] RhythmBrushes = new[]
		{
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.Red.R, Colors.Red.G, Colors.Red.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.Blue.R, Colors.Blue.G, Colors.Blue.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.Pink.R, Colors.Pink.G, Colors.Pink.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, 128,0,128)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.OrangeRed.R, Colors.OrangeRed.G, Colors.OrangeRed.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, 72,209,204)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, Colors.Green.R, Colors.Green.G, Colors.Green.B)),
			BrushHelper.CreateSolidColorBrush(Color.FromArgb(255, 138,43,226)),
		};

		public object Convert(object values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values is int beatRhythm && beatRhythm is not 0)
			{
				return RhythmBrushes[(beatRhythm - 1) % RhythmBrushes.Length];
			}

			return DefaultRhythmBrush;
		}

		public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
