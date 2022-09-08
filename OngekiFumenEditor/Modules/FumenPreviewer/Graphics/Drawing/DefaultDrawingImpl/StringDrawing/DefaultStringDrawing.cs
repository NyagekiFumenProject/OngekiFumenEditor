using FontStashSharp;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing
{
    [Export(typeof(IStringDrawing))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultStringDrawing : IStringDrawing, IDisposable
    {
        private Renderer renderer;

        private class FontHandle : IStringDrawing.FontHandle
        {
            public string Name { get; set; }
            public string FilePath { get; set; }
        }

        public static IEnumerable<IStringDrawing.FontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
        public static IStringDrawing.FontHandle DefaultFont { get; } = GetSupportFonts().FirstOrDefault(x => x.Name.ToLower() == "consola");

        public IEnumerable<IStringDrawing.FontHandle> SupportFonts { get; } = DefaultSupportFonts;

        private static IEnumerable<IStringDrawing.FontHandle> GetSupportFonts()
        {
            return Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new FontHandle
            {
                Name = Path.GetFileNameWithoutExtension(x),
                FilePath = x
            }).Where(x => Path.GetExtension(x.FilePath).ToLower() == ".ttf").ToArray();
        }

        public DefaultStringDrawing()
        {
            renderer = new Renderer();
        }

        Dictionary<IStringDrawing.FontHandle, FontSystem> cacheFonts = new Dictionary<IStringDrawing.FontHandle, FontSystem>();

        public FontSystem GetFontSystem(IStringDrawing.FontHandle fontHandle)
        {
            if (!cacheFonts.TryGetValue(fontHandle, out var fontSystem))
            {
                var handle = fontHandle as FontHandle;
                fontSystem = new FontSystem(new FontSystemSettings
                {
                    FontResolutionFactor = 2,
                    KernelWidth = 2,
                    KernelHeight = 2
                });
                fontSystem.AddFont(File.ReadAllBytes(handle.FilePath));
                cacheFonts[fontHandle] = fontSystem;
            }

            return fontSystem;
        }

        public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IFumenPreviewer target, IStringDrawing.FontHandle handle, out Vector2? measureTextSize)
        {
            measureTextSize = default;

            handle = handle ?? DefaultFont;

            if (style != 0)
                throw new NotSupportedException($"DefaultStringDrawing.Draw().style must be Normal only.");

            renderer.Begin(OpenTK.Mathematics.Matrix4.CreateTranslation(new(0, -target.ViewHeight / 2, 0)) * target.ViewProjectionMatrix);
            var font = GetFontSystem(handle).GetFont(fontSize);
            var size = font.MeasureString(text, scale);
            origin.X = origin.X * 2;
            origin = origin * size;
            scale.Y = -scale.Y;

            font.DrawText(renderer, text, pos, new FSColor(color.X, color.Y, color.Z, color.W), scale, rotate, origin);
            measureTextSize = size;
            renderer.End();
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
