using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Enemy Set", "Ongeki Objects")]
    public class EnemySetViewModel : DisplayTextLineObjectViewModelBase<EnemySet>
    {
        public override Brush DisplayBrush => Brushes.Yellow;
        private static BindingBase SharedDisplayValueBinding = new Binding("ReferenceOngekiObject.TagTblValue.Value");
        public override BindingBase DisplayValueBinding => SharedDisplayValueBinding;
    }
}
