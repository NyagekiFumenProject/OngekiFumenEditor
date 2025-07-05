// sources fetched and modified from https://github.com/mono/SkiaSharp/blob/main/tests/Tests/
using SkiaSharp.Internals;
using System;

namespace SkiaSharp.Tests
{
	public abstract class GlContext : IDisposable
	{
		public abstract void MakeCurrent();
		public abstract void SwapBuffers();
		public abstract void Destroy();
		public abstract GRGlTextureInfo CreateTexture(SKSizeI textureSize);
		public abstract void DestroyTexture(uint texture);

		void IDisposable.Dispose() => Destroy();

        public static GlContext Create()
		{
            if (PlatformConfiguration.IsLinux)
                return new GlxContext();
            else if (PlatformConfiguration.IsMac)
                return new CglContext();
            else if (PlatformConfiguration.IsWindows)
                return new WglContext();

			throw new NotSupportedException("Unknown platform for creating GlContext.");
        }
	}
}
