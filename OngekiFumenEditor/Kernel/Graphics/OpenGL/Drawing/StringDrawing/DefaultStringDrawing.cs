using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String.Platform;
using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing
{
    internal class DefaultStringDrawing : CommonOpenGLDrawingBase, IStringDrawing, IDisposable
    {
        private Renderer renderer;

        private class FontHandle : IStringDrawing.IFontHandle
        {
            public string FamilyName { get; set; }
            public string FilePath { get; set; }
        }

        public static IEnumerable<IStringDrawing.IFontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public static IStringDrawing.IFontHandle DefaultFont { get; } = GetSupportFonts().FirstOrDefault(x => x.FamilyName.ToLower() == "consola");

        public IEnumerable<IStringDrawing.IFontHandle> SupportFonts { get; } = DefaultSupportFonts;

        private static IEnumerable<IStringDrawing.IFontHandle> GetSupportFonts()
        {
            return Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new FontHandle
            {
                FamilyName = Path.GetFileNameWithoutExtension(x),
                FilePath = x
            }).Where(x => Path.GetExtension(x.FilePath).ToLower() == ".ttf").ToArray();
        }

        public DefaultStringDrawing(DefaultOpenGLRenderManager manager) : base(manager)
        {
            renderer = new Renderer();
        }

        Dictionary<IStringDrawing.IFontHandle, FontSystem> cacheFonts = new Dictionary<IStringDrawing.IFontHandle, FontSystem>();
        private DefaultOpenGLRenderManager defaultDrawingManager;

        public FontSystem GetFontSystem(IStringDrawing.IFontHandle fontHandle)
        {
            if (!cacheFonts.TryGetValue(fontHandle, out var fontSystem))
            {
                var settings = new FontSystemSettings
                {
                    FontResolutionFactor = 2,
                    KernelWidth = 1,
                    KernelHeight = 1,
                };
                fontSystem = new FontSystem(settings);

                if (ProgramSetting.Default.DisableStringRendererAntialiasing)
                {
                    var baseGlyphRenderer = settings.GlyphRenderer;
                    settings.GlyphRenderer = (i, o, opt) =>
                    {
                        baseGlyphRenderer(i, o, opt);
                        OnDisableAntialiasingGlyphRenderer(i, o, opt);
                    };
                }

                var handle = fontHandle as FontHandle;
                fontSystem.AddFont(File.ReadAllBytes(handle.FilePath));
                cacheFonts[fontHandle] = fontSystem;

                Log.LogDebug($"Created new FontSystem: {handle.FamilyName}, FilePath: {handle.FilePath}");
            }

            return fontSystem;
        }

        public void RebuildFontSystem()
        {
            foreach (var fontSystem in cacheFonts.Values)
                fontSystem.Dispose();
            cacheFonts.Clear();
        }

        private void OnDisableAntialiasingGlyphRenderer(byte[] input, byte[] output, GlyphRenderOptions options)
        {
            var size = options.Size.X * options.Size.Y;

            for (var i = 0; i < size; i++)
            {
                var c = input[i];
                var ci = i * 4;

                if (c == 0)
                {
                    output[ci] = output[ci + 1] = output[ci + 2] = output[ci + 3] = 0;
                }
                else
                {
                    output[ci] = output[ci + 1] = output[ci + 2] = output[ci + 3] = 255;
                }
            }
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IDrawingContext target, IStringDrawing.IFontHandle handle, out Vector2? measureTextSize)
        {
            target.PerfomenceMonitor.OnBeginDrawing(this);
            {
                handle = handle ?? DefaultFont;

                var fontStyle = TextStyle.None;

                IStringDrawing.IFontHandle GetSubFont(IStringDrawing.IFontHandle handle, string sub)
                {
                    var boldFontName = handle.FamilyName + "b";
                    return SupportFonts.FirstOrDefault(x => x.FamilyName == boldFontName);
                }

                if (style.HasFlag(IStringDrawing.StringStyle.Underline))
                    fontStyle = TextStyle.Underline;
                if (style.HasFlag(IStringDrawing.StringStyle.Strike))
                    fontStyle = TextStyle.Strikethrough;
                if (style.HasFlag(IStringDrawing.StringStyle.Bold))
                {
                    if (GetSubFont(handle, "b") is IStringDrawing.IFontHandle sb)
                        handle = sb;
                }
                if (style.HasFlag(IStringDrawing.StringStyle.Italic))
                {
                    if (GetSubFont(handle, "i") is IStringDrawing.IFontHandle sb)
                        handle = sb;
                }
                if (style.HasFlag(IStringDrawing.StringStyle.Italic) && style.HasFlag(IStringDrawing.StringStyle.Bold))
                {
                    if (GetSubFont(handle, "z") is IStringDrawing.IFontHandle sb)
                        handle = sb;
                }

                renderer.Begin(GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext), target.PerfomenceMonitor, this);
                var font = GetFontSystem(handle).GetFont(fontSize);
                var size = font.MeasureString(text, scale);
                origin.X = origin.X * 2;
                origin = origin * size;
                scale.Y = -scale.Y;

                font.DrawText(renderer, text, pos, new FSColor(color.X, color.Y, color.Z, color.W), rotate, origin, scale, textStyle: fontStyle);
                measureTextSize = size;
                renderer.End();
            }
            target.PerfomenceMonitor.OnAfterDrawing(this);
        }

        public void Dispose()
        {
            foreach (var fs in cacheFonts)
                fs.Value?.Dispose();
            cacheFonts.Clear();

            renderer?.Dispose();
            renderer = null;
        }
    }
}
