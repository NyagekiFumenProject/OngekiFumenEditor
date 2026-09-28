namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 离屏目标的 alpha 类型。
    /// Skia 后端：<see cref="Unpremul"/> 不支持（官方限定仅输入图像，渲染无法输出该类型），创建时抛 <see cref="System.NotSupportedException"/>。
    /// OpenGL 后端：没有 alpha 类型概念，该值仅记录在 <see cref="IOffscreenRenderContext.Options"/> 中，不影响渲染。
    /// </summary>
    public enum OffscreenAlphaType
    {
        /// <summary>颜色分量已预乘 alpha（渲染目标的自然格式，默认值）。</summary>
        Premul,

        /// <summary>颜色分量未预乘（Skia 不支持作为渲染输出）。</summary>
        Unpremul,

        /// <summary>所有像素不透明。</summary>
        Opaque,
    }
}
