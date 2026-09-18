using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonDrawTargetBase : IFumenEditorDrawingTarget
    {
        protected IFumenEditorDrawingContext target;
        protected IDrawCommandListBuilder builder;

        public abstract IEnumerable<string> DrawTargetID { get; }
        public abstract int DefaultRenderOrder { get; }
        public virtual DrawingVisible DefaultVisible => DrawingVisible.All;

        private int? currentRenderOrder = default;
        public int CurrentRenderOrder
        {
            get => currentRenderOrder ?? DefaultRenderOrder; set
            {
                currentRenderOrder = value;
            }
        }

        private DrawingVisible? currentVisible = default;
        public DrawingVisible Visible
        {
            get => currentVisible ?? DefaultVisible; set
            {
                currentVisible = value;
            }
        }

        public virtual void Begin(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            this.target = target;
            this.builder = builder;
        }

        public abstract void Post(OngekiObjectBase ongekiObject);

        public virtual void End()
        {
            target = default;
            builder = default;
        }

        public abstract void Initialize(IRenderManagerImpl impl);
    }

    public abstract class CommonDrawTargetBase<T> : CommonDrawTargetBase where T : OngekiObjectBase
    {
        public abstract void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, T obj);
        public override void Post(OngekiObjectBase ongekiObject) => Draw(target, builder, (T)ongekiObject);
    }

    public abstract class CommonBatchDrawTargetBase<T> : CommonDrawTargetBase where T : OngekiObjectBase
    {
        private List<T> drawObjects = new();

        public abstract void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<T> objs);

        public override void End()
        {
            DrawBatch(target, builder, drawObjects);
            drawObjects.Clear();

            base.End();
        }

        public override void Post(OngekiObjectBase ongekiObject)
        {
            drawObjects.Add((T)ongekiObject);
        }
    }
}
