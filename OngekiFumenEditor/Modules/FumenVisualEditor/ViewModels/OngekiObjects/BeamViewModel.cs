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
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Beam", "Ongeki Objects")]
    public class BeamViewModel : DisplayObjectViewModelBase<Beam>
    {
        protected override void OnAttachedView(object view)
        {
            base.OnAttachedView(view);
            //todo


        }
    }
}
