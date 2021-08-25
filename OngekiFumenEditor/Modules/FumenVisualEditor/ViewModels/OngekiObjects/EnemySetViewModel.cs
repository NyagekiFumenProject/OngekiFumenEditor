using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "EnemySet", "Ongeki Objects")]
    public class EnemySetViewModel : DisplayTextLineObjectViewModelBase<EnemySet>
    {
        public override Brush DisplayBrush => Brushes.Yellow;
        public override object DisplayValue => ReferenceOngekiObject?.TagTblValue?.Value;
    }
}
