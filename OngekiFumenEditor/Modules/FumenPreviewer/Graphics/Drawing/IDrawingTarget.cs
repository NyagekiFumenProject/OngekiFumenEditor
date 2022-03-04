using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IDrawingTarget
    {
        string DrawTargetID { get; }

        void BeginDraw();
        void Draw(OngekiObjectBase ongekiObject,OngekiFumen fumen);
        void EndDraw();
    }
}
