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
    public class LaneBlockAreaViewModel : DisplayTextLineObjectViewModelBase<LaneBlockArea>
    {
        public static Brush Brush { get; } = new SolidColorBrush(Colors.HotPink);
        public override Brush DisplayBrush { get; } = Brush;
    }

    public class LaneBlockAreaEndIndicatorViewModel : DisplayTextLineObjectViewModelBase<LaneBlockAreaEndIndicator>
    {
        public override Brush DisplayBrush { get; } = LaneBlockAreaViewModel.Brush;
    }
}
