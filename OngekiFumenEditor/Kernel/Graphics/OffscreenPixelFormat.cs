namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 离屏渲染目标的像素格式。
    /// 后端不支持请求的格式时，<c>CreateOffscreenToImage</c> 抛 <see cref="System.NotSupportedException"/>。
    /// </summary>
    public enum OffscreenPixelFormat
    {
        /// <summary>后端平台默认格式（Skia：<c>SKImageInfo.PlatformColorType</c>；OpenGL：RGBA8）。</summary>
        PlatformDefault,

        /// <summary>RGBA 8-8-8-8。</summary>
        Rgba8888,

        /// <summary>BGRA 8-8-8-8。OpenGL 后端不支持（桌面 core GL 无标准 BGRA8 内部格式）。</summary>
        Bgra8888,

        /// <summary>RGBA 10-10-10-2（OpenGL：<c>GL_RGB10_A2</c>）。</summary>
        Rgba1010102,

        /// <summary>RGBA 16 位半精度浮点。OpenGL 后端需要 <c>EXT_color_buffer_float</c>。</summary>
        RgbaF16,

        /// <summary>RGBA 32 位浮点。OpenGL 后端需要 <c>EXT_color_buffer_float</c>。</summary>
        RgbaF32,

        /// <summary>单通道 8 位灰度。OpenGL 后端不支持（R8 采样只出红通道，与灰度语义不一致）。</summary>
        Gray8,

        /// <summary>仅 alpha 通道。OpenGL 后端不支持。</summary>
        Alpha8,
    }
}
