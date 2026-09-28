using System;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Creation parameters for an offscreen render target.
    /// They are read back verbatim from <see cref="IOffscreenRenderContext.Options"/>; the read-back value is the requested value, not what the backend actually applied.
    /// </summary>
    public sealed record OffscreenRenderOptions
    {
        /// <summary>Target pixel width (device pixels, must be greater than 0).</summary>
        public required int Width { get; init; }

        /// <summary>Target pixel height (device pixels, must be greater than 0).</summary>
        public required int Height { get; init; }

        /// <summary>Pixel format, defaults to the backend platform default format.</summary>
        public OffscreenPixelFormat PixelFormat { get; init; } = OffscreenPixelFormat.PlatformDefault;

        /// <summary>Alpha type, defaults to premultiplied (<see cref="OffscreenAlphaType.Premul"/>).</summary>
        public OffscreenAlphaType AlphaType { get; init; } = OffscreenAlphaType.Premul;

        /// <summary>Color space, defaults to sRGB.</summary>
        public OffscreenColorSpace ColorSpace { get; init; } = OffscreenColorSpace.Srgb;

        /// <summary>
        /// When true, every submitted render requires the frame state of the command list to satisfy
        /// <c>ViewWidth * RenderScaleX ≈ Width</c> and <c>ViewHeight * RenderScaleY ≈ Height</c>;
        /// otherwise that render task fails with <see cref="ArgumentException"/> (tolerance 0.5 pixels).
        /// Defaults to false, which lets the caller do tiled/partial rendering through its own projection matrix.
        /// </summary>
        public bool RequireMatchingViewport { get; init; }

        /// <summary>Validates the creation parameters; called by the backend when creating an offscreen context.</summary>
        internal void Validate()
        {
            if (Width <= 0)
                throw new ArgumentOutOfRangeException(nameof(Width), Width, "Offscreen target width must be greater than 0.");
            if (Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(Height), Height, "Offscreen target height must be greater than 0.");
            if (!Enum.IsDefined(PixelFormat))
                throw new ArgumentOutOfRangeException(nameof(PixelFormat), PixelFormat, "Unknown pixel format.");
            if (!Enum.IsDefined(AlphaType))
                throw new ArgumentOutOfRangeException(nameof(AlphaType), AlphaType, "Unknown alpha type.");
            if (!Enum.IsDefined(ColorSpace))
                throw new ArgumentOutOfRangeException(nameof(ColorSpace), ColorSpace, "Unknown color space.");
        }

        /// <summary>Validates the command list frame state against <see cref="RequireMatchingViewport"/>; throws <see cref="ArgumentException"/> on mismatch.</summary>
        internal void EnsureViewportMatches(in DrawCommandListFrameState frameState)
        {
            if (!RequireMatchingViewport)
                return;

            var width = frameState.ViewWidth * frameState.RenderScaleX;
            var height = frameState.ViewHeight * frameState.RenderScaleY;
            if (Math.Abs(width - Width) > 0.5f || Math.Abs(height - Height) > 0.5f)
                throw new ArgumentException($"Command list viewport ({width}x{height}) does not match the offscreen target ({Width}x{Height}).");
        }
    }
}
