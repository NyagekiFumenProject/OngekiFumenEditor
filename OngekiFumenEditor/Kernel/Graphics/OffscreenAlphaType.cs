namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Alpha type of the offscreen target.
    /// Skia backend: <see cref="Unpremul"/> is not supported (upstream restricts it to input images; rendering cannot output it) and throws <see cref="System.NotSupportedException"/> at creation time.
    /// OpenGL backend: there is no alpha type concept; the value is only recorded in <see cref="IOffscreenRenderContext.Options"/> and does not affect rendering.
    /// </summary>
    public enum OffscreenAlphaType
    {
        /// <summary>Color components are premultiplied by alpha (the natural format for a render target, the default).</summary>
        Premul,

        /// <summary>Color components are not premultiplied (Skia does not support it as render output).</summary>
        Unpremul,

        /// <summary>All pixels are opaque.</summary>
        Opaque,
    }
}
