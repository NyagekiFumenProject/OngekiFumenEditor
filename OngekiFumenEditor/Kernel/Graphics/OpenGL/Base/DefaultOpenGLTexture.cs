using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Base
{
    public sealed class DefaultOpenGLTexture : IImage, IDisposable
    {
        private int? _id;
        private Vector2 _textureSize;
        public string Name { get; init; }

        public int ID => _id ?? throw new ArgumentNullException(nameof(_id));
        public int Width => (int)_textureSize.X;
        public int Height => (int)_textureSize.Y;

        public TextureWrapMode TextureWrapS
        {
            get
            {
                GL.GetTextureParameter(ID, GetTextureParameter.TextureWrapS, out int m);
                return (TextureWrapMode)m;
            }
            set
            {
                GL.TextureParameter(ID, TextureParameterName.TextureWrapS, (int)value);
            }
        }

        public TextureWrapMode TextureWrapT
        {
            get
            {
                GL.GetTextureParameter(ID, GetTextureParameter.TextureWrapT, out int m);
                return (TextureWrapMode)m;
            }
            set
            {
                GL.TextureParameter(ID, TextureParameterName.TextureWrapT, (int)value);
            }
        }

        public TextureMinFilter TextureMinFilter
        {
            get
            {
                GL.GetTextureParameter(ID, GetTextureParameter.TextureMinFilter, out int m);
                return (TextureMinFilter)m;
            }
            set
            {
                GL.TextureParameter(ID, TextureParameterName.TextureMinFilter, (int)value);
            }
        }

        public TextureMagFilter TextureMagFilter
        {
            get
            {
                GL.GetTextureParameter(ID, GetTextureParameter.TextureMagFilter, out int m);
                return (TextureMagFilter)m;
            }
            set
            {
                GL.TextureParameter(ID, TextureParameterName.TextureMagFilter, (int)value);
            }
        }

        private readonly Action<int> releaseTexture;

        public DefaultOpenGLTexture(string name = "Texture")
        {
            Name = name;
        }

        /// <summary>
        /// Offscreen-only constructor: takes over an already created GL texture name.
        /// <see cref="Dispose"/> does not call <c>GL.DeleteTexture</c> directly; it releases the texture through
        /// <paramref name="releaseTexture"/> (deleting an offscreen texture must happen while a context is current, so
        /// callers usually pass the enqueue delegate of the delayed deletion queue).
        /// </summary>
        /// <param name="textureId">The existing GL texture name.</param>
        /// <param name="width">Texture width in device pixels.</param>
        /// <param name="height">Texture height in device pixels.</param>
        /// <param name="releaseTexture">Texture release delegate; when null, falls back to deleting directly.</param>
        /// <param name="name">Texture name (for debugging).</param>
        internal DefaultOpenGLTexture(int textureId, int width, int height, Action<int> releaseTexture, string name = "OffscreenTexture") : this(name)
        {
            _id = textureId;
            _textureSize = new Vector2(width, height);
            this.releaseTexture = releaseTexture;
        }

        public DefaultOpenGLTexture(Bitmap bmp, string name = "Texture") : this(name)
        {
            GL.GenTextures(1, out int id);
            _id = id;

            OpenGLTextureBindingCache.BindTexture2D(ID);

            TextureMinFilter = TextureMinFilter.Linear;
            TextureMagFilter = TextureMagFilter.Linear;
            TextureWrapS = TextureWrapMode.ClampToEdge;
            TextureWrapT = TextureWrapMode.ClampToEdge;

            _textureSize = new Vector2(bmp.Width, bmp.Height);

            var bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp_data.Width, bmp_data.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

            bmp.UnlockBits(bmp_data);
        }

        public override string ToString()
        {
            return $"({ID}){Name}";
        }

        public void Dispose()
        {
            if (_id is int id)
            {
                _id = null;

                if (releaseTexture is not null)
                {
                    releaseTexture(id);
                }
                else
                {
                    GL.DeleteTexture(id);
                    OpenGLTextureBindingCache.InvalidateTexture(id);
                }
            }
        }
    }
}
