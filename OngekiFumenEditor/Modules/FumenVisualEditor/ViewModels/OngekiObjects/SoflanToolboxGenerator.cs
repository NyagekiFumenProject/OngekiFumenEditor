using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static OngekiFumenEditor.Base.OngekiObjects.LaneBlockArea;
using static OngekiFumenEditor.Base.OngekiObjects.Soflan;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{   
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Soflan", "Lane Control")]
    public class SoflanToolboxGenerator : ToolboxGenerator<Soflan>
    {

    }
}
