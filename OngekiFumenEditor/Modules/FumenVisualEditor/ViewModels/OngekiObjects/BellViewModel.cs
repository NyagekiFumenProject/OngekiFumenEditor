using Gemini.Modules.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Bell", "Ongeki Objects")]
    public class BellViewModel : DisplayObjectViewModelBase
    {
        public override string ObjectType => "bell";
    }
}
