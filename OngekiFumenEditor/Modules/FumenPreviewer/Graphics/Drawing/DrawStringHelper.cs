using FontStashSharp;
using FontStashSharp.Interfaces;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.String.Platform;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenPreviewer.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public class DrawStringHelper : IDisposable
    {
        private Renderer renderer;
        private FontSystem fontSystem;

        public DrawStringHelper()
        {
            renderer = new Renderer();
            fontSystem = new FontSystem(new FontSystemSettings
            {
                FontResolutionFactor = 2,
                KernelWidth = 2,
                KernelHeight = 2
            });

            var supportFonts = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new
            {
                Name = Path.GetFileNameWithoutExtension(x),
                FilePath = x
            }).Where(x => Path.GetExtension(x.FilePath).ToLower() == ".ttf").ToArray();

            fontSystem.AddFont(File.ReadAllBytes(supportFonts.FirstOrDefault(x => x.Name == "consola").FilePath));
        }

        public void Begin(IFumenPreviewer fumenPreviewer)
        {
            renderer.Begin(Matrix4.CreateTranslation(new(0, -fumenPreviewer.ViewHeight / 2, 0)) * fumenPreviewer.ViewProjectionMatrix);
        }

        public void Dispose()
        {
            renderer?.Dispose();
            renderer = null;
        }

        public Vector2 Draw(string text, Vector2 pos, Vector2 scale, float rotate, int fontSize, Vector4? color = default, Vector2? norigin = default)
            => Draw(text, pos, scale, rotate, fontSize, color, norigin);
        public Vector2 Draw(string text, Vector2 pos, Vector2 scale, float rotate, int fontSize, FSColor? color = default, Vector2? norigin = default)
        {
            var font = fontSystem.GetFont(fontSize);
            var size = font.MeasureString(text, scale);
            var no = (norigin ?? new Vector2(0.5f, 0.5f));
            no.X = no.X * 2;
            var origin = no * size;
            scale.Y = -scale.Y;

            font.DrawText(renderer, text, pos, color ?? FSColor.White, scale, rotate, origin);
            return size;
        }

        public void End()
        {
            renderer.End();
        }
    }
}
