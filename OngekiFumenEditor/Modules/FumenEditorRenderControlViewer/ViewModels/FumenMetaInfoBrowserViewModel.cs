using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System.ComponentModel;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.ViewModels
{
    [Export(typeof(IFumenEditorRenderControlViewer))]
    public class FumenEditorRenderControlViewerViewModel : Tool, IFumenEditorRenderControlViewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        public FumenEditorRenderControlViewerViewModel()
        {
            DisplayName = "编辑器渲染控制";

            var targets = IoC.GetAll<IFumenEditorDrawingTarget>();
        }
    }
}
