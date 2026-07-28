using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Models
{
    public class ColorPropertyWrapper : ObservableObject
    {
        private readonly PropertyInfo propertyInfo;
        private readonly object owner;
        private SolidColorBrush cachedColorBrush;
        private SolidColorBrush cachedReverseColorBrush;

        public ColorPropertyWrapper(PropertyInfo propertyInfo, object owner)
        {
            this.propertyInfo = propertyInfo;
            this.owner = owner;

            RefreshBrush();
        }

        private void RefreshBrush()
        {
            var color = Color;
            cachedColorBrush = new SolidColorBrush(color.ToMediaColor());
            cachedReverseColorBrush = new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(color.A, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B)));

            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(ReverseBrush));
        }

        public System.Drawing.Color Color
        {
            get => (System.Drawing.Color)propertyInfo.GetValue(owner, null);
            set
            {
                propertyInfo.SetValue(owner, value, null);
                RefreshBrush();

                OnPropertyChanged(nameof(Color));
                OnPropertyChanged(nameof(ColorString));
            }
        }

        public SolidColorBrush Brush => cachedColorBrush;
        public SolidColorBrush ReverseBrush => cachedReverseColorBrush;

        public string Name => propertyInfo.Name;

        public string ColorString
        {
            get
            {
                var r = Color;
                return $"{r.R}, {r.G}, {r.B}, {r.A}";
            }
            set
            {
                var split = value.Split(',');
                var r = int.Parse(split[0].Trim());
                var g = int.Parse(split[1].Trim());
                var b = int.Parse(split[2].Trim());
                var a = split.Length > 3 ? int.Parse(split[3]) : 255;

                Color = System.Drawing.Color.FromArgb(a, r, g, b);
            }
        }
    }
}


