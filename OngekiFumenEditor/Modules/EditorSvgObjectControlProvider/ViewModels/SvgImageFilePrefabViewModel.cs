using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels
{
    [Gemini.Modules.Toolbox.ToolboxItem(typeof(FumenVisualEditorViewModel), "SvgPrefab(File)", "Misc")]
    [MapToView(ViewType = typeof(SvgPrefabView))]
    public class SvgImageFilePrefabViewModel : SvgPrefabViewModelBase<SvgImageFilePrefab>
    {
    }
}
