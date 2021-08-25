using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Meter Change", "Ongeki Objects")]
    public class MeterChangeViewModel : DisplayTextLineObjectViewModelBase<MeterChange>
    {
        public override Brush DisplayBrush => Brushes.LightGreen;
        public override object DisplayValue => $"{ReferenceOngekiObject.Bunbo} {ReferenceOngekiObject.BunShi}";
    }
}
