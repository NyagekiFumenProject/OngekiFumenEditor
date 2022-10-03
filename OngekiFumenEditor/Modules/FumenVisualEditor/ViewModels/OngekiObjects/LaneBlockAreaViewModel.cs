using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static OngekiFumenEditor.Base.OngekiObjects.LaneBlockArea;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{   
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Block", "Lane Control")]
    public class LaneBlockAreaViewModel : ToolboxGenerator<LaneBlockArea>
    {

    }

    public class LaneBlockAreaEndIndicatorViewModel : ToolboxGenerator<LaneBlockAreaEndIndicator>
    {

    }
}
