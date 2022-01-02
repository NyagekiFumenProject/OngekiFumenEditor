using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "BPM Change", "Ongeki Objects")]
    public class BPMChangeViewModel : DisplayTextLineObjectViewModelBase<BPMChange>
    {
        public override Brush DisplayBrush => Brushes.Pink;
        private static BindingBase SharedDisplayValueBinding = new Binding("ReferenceOngekiObject.BPM");
        public override BindingBase DisplayValueBinding => SharedDisplayValueBinding;

        public override void OnDragEnd(Point pos)
        {
            base.OnDragEnd(pos);
            //when bpmChange drag done. all ongeki object need to redraw to adapt new bpmList changed.
            EditorViewModel.Redraw(FumenVisualEditorViewModel.RedrawTarget.OngekiObjects);
        }
    }
}
