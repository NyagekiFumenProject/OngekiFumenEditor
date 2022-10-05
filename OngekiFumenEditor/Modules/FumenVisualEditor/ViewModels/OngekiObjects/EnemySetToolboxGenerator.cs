using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Enemy Set", "Ongeki Objects")]
    public class EnemySetToolboxGenerator : ToolboxGenerator<EnemySet>
    {

    }
}
