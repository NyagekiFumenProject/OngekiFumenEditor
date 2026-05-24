using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing.String.Platform;
using OngekiFumenEditor.Kernel.Graphics.Text;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    internal sealed class DefaultStringDrawing : CommonOpenGLDrawingBase, IStringDrawing, IDisposable
    {
        private readonly DefaultStringMeasure stringMeasure = new();
        private Renderer renderer;

        public static IEnumerable<IFontHandle> DefaultSupportFonts => DefaultStringMeasure.DefaultSupportFonts;
        public static IFontHandle DefaultFont => DefaultStringMeasure.DefaultFont;

        public IEnumerable<IFontHandle> SupportFonts => stringMeasure.SupportFonts;

        public DefaultStringDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
            renderer = new Renderer();
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize)
        {
            text ??= string.Empty;
            handle ??= DefaultStringMeasure.DefaultFont;

            var resolvedStyle = stringMeasure.ResolveTextStyle(handle, style);
            var font = stringMeasure.GetFontSystem(resolvedStyle.FontHandle).GetFont(fontSize);
            var size = stringMeasure.MeasureString(font, text, resolvedStyle.FontHandle, fontSize, scale, resolvedStyle.FontStyle);

            renderer.Begin(GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext), target.RenderContext.PerfomenceMonitor);
            origin.X *= 2;
            origin *= size;
            scale.Y = -scale.Y;

            font.DrawText(renderer, text, pos, new FSColor(color.X, color.Y, color.Z, color.W), rotate, origin, scale, textStyle: resolvedStyle.FontStyle);
            measureTextSize = size;
            renderer.End();
        }

        public void Dispose()
        {
            renderer?.Dispose();
            renderer = null;
        }
    }
}
