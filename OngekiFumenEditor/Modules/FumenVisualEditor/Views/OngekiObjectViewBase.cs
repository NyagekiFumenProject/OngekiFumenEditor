using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.UI.ValueConverters;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
    public class OngekiObjectViewBase : UserControl
    {
        public DisplayObjectViewModelBase ViewModel => DataContext as DisplayObjectViewModelBase;
        private static DropShadowEffect SelectEffect = new DropShadowEffect() { ShadowDepth = 0, Color = Colors.Yellow, BlurRadius = 25 };
        private readonly static LambdaConverter<bool, Effect> isSelectConverter = new(o => o ? SelectEffect : default);

        public OngekiObjectViewBase()
        {
            SetBinding(EffectProperty, new Binding("IsSelected")
            {
                Converter = isSelectConverter
            });
        }
    }
}
