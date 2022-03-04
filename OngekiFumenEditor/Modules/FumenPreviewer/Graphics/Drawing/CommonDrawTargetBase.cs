using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonDrawTargetBase<T> : IDrawingTarget where T : OngekiObjectBase
    {
        public abstract string DrawTargetID { get; }

        public virtual void BeginDraw()
        {

        }

        public void Draw(OngekiObjectBase ongekiObject, OngekiFumen fumen) => Draw((T)ongekiObject, fumen);

        public abstract void Draw(T ongekiObject, OngekiFumen fumen);

        public virtual void EndDraw()
        {

        }
    }
}
