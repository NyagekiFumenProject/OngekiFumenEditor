using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Converters
{
    public class BeatRhythmToBrushConverter : IValueConverter
    {
        public static readonly Brush DefaultRhythmBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

        public static readonly Brush[] RhythmBrushes = new[]
        {
            new SolidColorBrush(Color.FromArgb(255, Colors.Red.R, Colors.Red.G, Colors.Red.B)),
            new SolidColorBrush(Color.FromArgb(255, Colors.Blue.R, Colors.Blue.G, Colors.Blue.B)),
            new SolidColorBrush(Color.FromArgb(255, Colors.Pink.R, Colors.Pink.G, Colors.Pink.B)),
            new SolidColorBrush(Color.FromArgb(255, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B)),
            new SolidColorBrush(Color.FromArgb(255, 128,0,128)),
            new SolidColorBrush(Color.FromArgb(255, Colors.OrangeRed.R, Colors.OrangeRed.G, Colors.OrangeRed.B)),
            new SolidColorBrush(Color.FromArgb(255, 72,209,204)),
            new SolidColorBrush(Color.FromArgb(255, Colors.Green.R, Colors.Green.G, Colors.Green.B)),
            new SolidColorBrush(Color.FromArgb(255, 138,43,226)),
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
