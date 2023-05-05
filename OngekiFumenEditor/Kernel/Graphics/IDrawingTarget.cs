using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingTarget
    {
        IEnumerable<string> DrawTargetID { get; }
        int DefaultRenderOrder { get; }
        int CurrentRenderOrder { get; set; }

        void Begin(IFumenEditorDrawingContext target);
        void Post(OngekiObjectBase ongekiObject);
        void End();
    }
}