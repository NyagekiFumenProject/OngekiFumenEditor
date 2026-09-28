
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

        protected SKCanvas Canvas => canvas;

        protected virtual bool OnBegin(IDrawingContext target)
        {
            SkiaUtility.CheckSkiaRenderContext(target?.RenderContext);

            if (target?.RenderContext is not ISkiaRenderContext skia || skia.Canvas is not { } targetCanvas)
            {
                this.target = default;
                canvas = default;
                return false;
            }

            this.target = target;
            canvas = targetCanvas;
            canvas.Save();

            var mvp = (GetOverrideModelMatrix() * GetOverrideViewMatrixOrDefault(target.CurrentDrawingTargetContext)).ToSkiaMatrix44();

            var ctx = target.CurrentDrawingTargetContext;
            var adjustMVP = mvp
                * SKMatrix44.CreateScale(1, -1, 1)
                * SKMatrix44.CreateTranslation(ctx.ViewWidth / 2, ctx.ViewHeight / 2, 0)
                * SKMatrix44.CreateScale(ctx.RenderScaleX, ctx.RenderScaleY, 1);

            canvas.SetMatrix(adjustMVP);
            return true;
        }


        protected virtual void OnEnd()
        {
            canvas?.Restore();

            target = default;
            canvas = default;
        }
    }
}
