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
    public class SoflanViewModel : DisplayTextLineObjectViewModelBase<Soflan>
    {
        public static Brush Brush { get; } = Brushes.LightCyan;
        public override Brush DisplayBrush { get; } = Brush;
    }

    public class SoflanEndIndicatorViewModel : DisplayTextLineObjectViewModelBase<SoflanEndIndicator>
    {
        public override Brush DisplayBrush { get; } = SoflanViewModel.Brush;
    }
}
