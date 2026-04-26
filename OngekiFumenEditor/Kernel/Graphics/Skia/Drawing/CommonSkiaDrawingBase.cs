
using OngekiFumenEditor.Utils;
using SkiaSharp;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.Drawing
{
    public class CommonSkiaDrawingBase : CommonDrawingBase
    {
        protected DefaultSkiaDrawingManagerImpl manager;
        private IDrawingContext target;
        private SKCanvas canvas;

        public CommonSkiaDrawingBase(DefaultSkiaDrawingManagerImpl manager)
        {
            this.manager = manager;
        }

        protected virtual void OnBegin(IDrawingContext target)
        {
            SkiaUtility.CheckSkiaRenderContext(target?.RenderContext);

            target.PerfomenceMonitor.OnBeginDrawing(this);
            this.target = target;
            canvas = ((DefaultSkiaRenderContext)target.RenderContext).Canvas;
            canvas.Save();

            var mvp = (GetOverrideModelMatrix() * GetOverrideViewMatrixOrDefault(target.CurrentDrawingTargetContext)).ToSkiaMatrix44();

            var ctx = target.CurrentDrawingTargetContext;
            var adjustMVP = mvp
                * SKMatrix44.CreateScale(1, -1, 1)
                * SKMatrix44.CreateTranslation(ctx.ViewWidth / 2, ctx.ViewHeight / 2, 0)
                * SKMatrix44.CreateScale(ctx.RenderScaleX, ctx.RenderScaleY, 1);

            canvas.SetMatrix(adjustMVP);
        }


        protected virtual void OnEnd()
        {
            canvas.Restore();

            target.PerfomenceMonitor.OnAfterDrawing(this);
            target = default;
            canvas = default;
        }
    }
}
