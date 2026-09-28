namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Pixel format of the offscreen render target.
    /// When a backend does not support the requested format, <c>CreateOffscreenToImage</c> throws <see cref="System.NotSupportedException"/>.
    /// </summary>
    public enum OffscreenPixelFormat
    {
        /// <summary>Backend platform default format (Skia: <c>SKImageInfo.PlatformColorType</c>; OpenGL: RGBA8).</summary>
        PlatformDefault,

        /// <summary>RGBA 8-8-8-8.</summary>
        Rgba8888,

        /// <summary>BGRA 8-8-8-8. Not supported by the OpenGL backend (desktop core GL has no standard BGRA8 internal format).</summary>
        Bgra8888,

        /// <summary>RGBA 10-10-10-2 (OpenGL: <c>GL_RGB10_A2</c>).</summary>
        Rgba1010102,

        /// <summary>RGBA 16-bit half float. The OpenGL backend requires <c>EXT_color_buffer_float</c>.</summary>
        RgbaF16,

        /// <summary>RGBA 32-bit float. The OpenGL backend requires <c>EXT_color_buffer_float</c>.</summary>
        RgbaF32,

        /// <summary>Single-channel 8-bit grayscale. Not supported by the OpenGL backend (R8 sampling only returns the red channel, which does not match grayscale semantics).</summary>
        Gray8,

        /// <summary>Alpha channel only. Not supported by the OpenGL backend.</summary>
        Alpha8,
    }
}
