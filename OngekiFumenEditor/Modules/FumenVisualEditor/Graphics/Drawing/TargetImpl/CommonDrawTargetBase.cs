using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonDrawTargetBase<T> : IDrawingTarget where T : OngekiObjectBase
    {
        public abstract IEnumerable<string> DrawTargetID { get; }
        public abstract int DefaultRenderOrder { get; }

        private IFumenEditorDrawingContext target;
        private IPerfomenceMonitor performenceMonitor;

        public CommonDrawTargetBase()
        {
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();
        }

        public virtual void Begin(IFumenEditorDrawingContext target)
        {
            performenceMonitor.OnBeginTargetDrawing(this);
            this.target = target;
        }

        public abstract void Draw(IFumenEditorDrawingContext target, T obj);

        public virtual void End()
        {
            target = default;
            performenceMonitor.OnAfterTargetDrawing(this);
        }

        public void Post(OngekiObjectBase ongekiObject)
        {
            Draw(target, (T)ongekiObject);
        }
    }

    public abstract class CommonBatchDrawTargetBase<T> : IDrawingTarget where T : OngekiObjectBase
    {
        public abstract IEnumerable<string> DrawTargetID { get; }
        public abstract int DefaultRenderOrder { get; }

        private IFumenEditorDrawingContext target;
        private List<T> drawObjects = new();
        private IPerfomenceMonitor performenceMonitor;

        public CommonBatchDrawTargetBase()
        {
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();
        }

        public virtual void Begin(IFumenEditorDrawingContext target)
        {
            performenceMonitor.OnBeginTargetDrawing(this);
            this.target = target;
        }

        public abstract void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> objs);

        public virtual void End()
        {
            DrawBatch(target, drawObjects);

            target = default;
            drawObjects.Clear();
            performenceMonitor.OnAfterTargetDrawing(this);
        }

        public void Post(OngekiObjectBase ongekiObject)
        {
            drawObjects.Add((T)ongekiObject);
        }
    }
}
