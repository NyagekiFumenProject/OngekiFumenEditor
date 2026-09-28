namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Color space of the offscreen target.
    /// It takes effect on the Skia backend; the OpenGL backend does no color management (never enables <c>GL_FRAMEBUFFER_SRGB</c>),
    /// where the value is only recorded in <see cref="IOffscreenRenderContext.Options"/> to keep the look consistent with on-screen GL rendering.
    /// </summary>
    public enum OffscreenColorSpace
    {
        /// <summary>sRGB (the default).</summary>
        Srgb,

        /// <summary>Linear sRGB transfer function.</summary>
        SrgbLinear,

        /// <summary>Display P3 gamut. The Skia backend builds it from the sRGB transfer function plus the Display P3 conversion matrix.</summary>
        DisplayP3,

        /// <summary>No color space information (Skia: <c>null</c>).</summary>
        Null,
    }
}
