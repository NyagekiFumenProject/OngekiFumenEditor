namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 离屏目标的颜色空间。
    /// Skia 后端会真实生效；OpenGL 后端不做颜色管理（不启用 <c>GL_FRAMEBUFFER_SRGB</c>），该值仅记录在
    /// <see cref="IOffscreenRenderContext.Options"/> 中，以保持与屏幕 GL 渲染一致的观感。
    /// </summary>
    public enum OffscreenColorSpace
    {
        /// <summary>sRGB（默认值）。</summary>
        Srgb,

        /// <summary>线性 sRGB 传输函数。</summary>
        SrgbLinear,

        /// <summary>Display P3 色域。Skia 后端以 sRGB 传输函数 + Display P3 转换矩阵构造。</summary>
        DisplayP3,

        /// <summary>无颜色空间信息（Skia：<c>null</c>）。</summary>
        Null,
    }
}
