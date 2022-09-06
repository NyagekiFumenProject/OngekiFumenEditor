using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonDrawTargetBase : IDrawingTarget
    {
        public abstract IEnumerable<string> DrawTargetID { get; }
        public IFumenPreviewer Previewer { get; set; }

        public void BeginDraw(IFumenPreviewer previewer)
        {
            Previewer = previewer;
            BeginDraw();
        }

        public virtual void BeginDraw()
        {

        }

        public abstract void Draw(OngekiObjectBase ongekiObject, OngekiFumen fumen);

        public virtual void EndDraw()
        {
            Previewer = default;
        }

        public void RegisterHitTest(OngekiObjectBase ongekiObject, Rect rect)
        {
            Previewer?.RegisterSelectableObject(ongekiObject, rect);
        }
    }

    public abstract class CommonDrawTargetBase<T> : CommonDrawTargetBase where T : OngekiObjectBase
    {
        public override void Draw(OngekiObjectBase ongekiObject, OngekiFumen fumen) => Draw((T)ongekiObject, fumen);

        public abstract void Draw(T ongekiObject, OngekiFumen fumen);
    }
}
