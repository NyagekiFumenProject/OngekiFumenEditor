
using OngekiFumenEditor.Avalonia.Utils;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing
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
            var renderContext = (DefaultSkiaRenderContext)target.RenderContext;
            canvas = renderContext.Canvas;
            canvas.Save();

            var mvp = (GetOverrideModelMatrix() * GetOverrideViewMatrixOrDefault(target.CurrentDrawingTargetContext)).ToSkiaMatrix44();
            var flip = SKMatrix44.CreateScale(1, -1, 1);
            var translation = SKMatrix44.CreateTranslation(
                target.CurrentDrawingTargetContext.ViewWidth / 2,
                target.CurrentDrawingTargetContext.ViewHeight / 2,
                0);
            var mvpWithFlip = SKMatrix44.Concat(mvp, flip);
            var adjustMVP = SKMatrix44.Concat(mvpWithFlip, translation);

            // The leased canvas already contains Avalonia's visual offset, clip and
            // DPI transform. Compose the editor matrix on top of that state instead
            // of replacing it with SetMatrix, which would move the control to the
            // surface origin and apply DPI twice.
            var adjustMatrix = adjustMVP.Matrix;
            canvas.Concat(ref adjustMatrix);
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


