using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "BPM Change", "Ongeki Objects")]
    public class BPMChangeToolboxGenerator : ToolboxGenerator<BPMChange>
    {

    }
}
