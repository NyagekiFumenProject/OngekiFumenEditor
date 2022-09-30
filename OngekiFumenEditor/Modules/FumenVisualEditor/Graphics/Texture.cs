using OpenTK.Graphics.OpenGL;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics
{
    [Serializable]
    public class Texture : IDisposable
    {
        protected int _id;

        [Browsable(false)]
        public int ID { get { return _id; } }

        private Vector2 _textureSize;

        public string Name { get; set; }

        public int Width { get { return (int)_textureSize.X; } }
        public int Height { get { return (int)_textureSize.Y; } }

        public Texture(string name = "Texture")
        {
            Name = name;
            _id = 0;
        }

        public Texture(Bitmap bmp, string name = "Texture") : this(name)
        {
            LoadFromFile(bmp);
        }

        public void LoadFromFile(Bitmap bmp)
        {
            GL.GenTextures(1, out _id);

            GL.BindTexture(TextureTarget.Texture2D, _id);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _textureSize = new Vector2(bmp.Width, bmp.Height);

            BitmapData bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp_data.Width, bmp_data.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

            bmp.UnlockBits(bmp_data);
        }

        public void LoadFromData(IntPtr data, int width, int height)
        {
            if (data != IntPtr.Zero)
            {
                GL.GenTextures(1, out _id);

                GL.BindTexture(TextureTarget.Texture2D, _id);

                _textureSize = new Vector2(width, height);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public void Dispose()
        {
            GL.DeleteTexture(_id);
        }
    }
}