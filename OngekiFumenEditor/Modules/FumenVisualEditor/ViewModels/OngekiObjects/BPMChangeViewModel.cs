using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "BPM Change", "Ongeki Objects")]
    public class BPMChangeViewModel : DisplayTextLineObjectViewModelBase<BPMChange>
    {
        public override Brush DisplayBrush => Brushes.Pink;
        public override object DisplayValue => ReferenceOngekiObject?.Value;
    }
}
