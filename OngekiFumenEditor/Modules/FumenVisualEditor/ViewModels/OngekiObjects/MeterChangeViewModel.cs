using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Meter Change", "Ongeki Objects")]
    public class MeterChangeViewModel : DisplayTextLineObjectViewModelBase<MeterChange>
    {
        public override Brush DisplayBrush => Brushes.LightGreen;

        private static MultiBinding ShareBinding = new MultiBinding()
        {
            StringFormat = "{0} {1}"
        };

        static MeterChangeViewModel()
        {
            ShareBinding.Bindings.Add(new Binding("ReferenceOngekiObject.BunShi"));
            ShareBinding.Bindings.Add(new Binding("ReferenceOngekiObject.Bunbo"));
        }

        public override BindingBase DisplayValueBinding => ShareBinding;
    }
}
