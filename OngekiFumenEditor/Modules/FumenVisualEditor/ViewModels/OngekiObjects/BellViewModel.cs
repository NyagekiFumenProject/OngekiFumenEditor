using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [Export(typeof(BellViewModel))]
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Bell", "Ongeki Objects")]
    public class BellViewModel : DisplayObjectViewModelBase<Bell>
    {

    }
}
