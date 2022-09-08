using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    public abstract class CommonDrawTargetBase<T> : IDrawingTarget where T : OngekiObjectBase
    {
        public abstract IEnumerable<string> DrawTargetID { get; }

        private IFumenPreviewer target;

        public virtual void Begin(IFumenPreviewer target)
        {
            this.target = target;
        }

        public abstract void Draw(IFumenPreviewer target, T obj);

        public virtual void End()
        {
            target = default;
        }

        public void Post(OngekiObjectBase ongekiObject)
        {
            Draw(target, (T)ongekiObject);
        }
    }

    public abstract class CommonBatchDrawTargetBase<T> : IDrawingTarget where T : OngekiObjectBase
    {
        public abstract IEnumerable<string> DrawTargetID { get; }

        private IFumenPreviewer target;
        private List<T> drawObjects = new();

        public virtual void Begin(IFumenPreviewer target)
        {
            this.target = target;
        }

        public abstract void DrawBatch(IFumenPreviewer target, IEnumerable<T> objs);

        public virtual void End()
        {
            DrawBatch(target, drawObjects);

            target = default;
            drawObjects.Clear();
        }

        public void Post(OngekiObjectBase ongekiObject)
        {
            drawObjects.Add((T)ongekiObject);
        }
    }
}
