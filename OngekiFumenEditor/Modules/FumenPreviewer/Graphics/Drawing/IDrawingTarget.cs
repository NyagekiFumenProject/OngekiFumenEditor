using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IDrawingTarget
    {
        IEnumerable<string> DrawTargetID { get; }
        int DefaultRenderOrder { get; }

        void Begin(IFumenPreviewer target);
        void Post(OngekiObjectBase ongekiObject);
        void End();
    }
}