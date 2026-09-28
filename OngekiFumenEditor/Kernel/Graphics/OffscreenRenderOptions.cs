using System;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 离屏渲染目标的创建参数。
    /// 创建后由 <see cref="IOffscreenRenderContext.Options"/> 原样回读，回读值代表「请求值」而非后端实际生效值。
    /// </summary>
    public sealed record OffscreenRenderOptions
    {
        /// <summary>目标像素宽度（设备像素，必须大于 0）。</summary>
        public required int Width { get; init; }

        /// <summary>目标像素高度（设备像素，必须大于 0）。</summary>
        public required int Height { get; init; }

        /// <summary>像素格式，默认后端平台默认格式。</summary>
        public OffscreenPixelFormat PixelFormat { get; init; } = OffscreenPixelFormat.PlatformDefault;

        /// <summary>alpha 类型，默认预乘（<see cref="OffscreenAlphaType.Premul"/>）。</summary>
        public OffscreenAlphaType AlphaType { get; init; } = OffscreenAlphaType.Premul;

        /// <summary>颜色空间，默认 sRGB。</summary>
        public OffscreenColorSpace ColorSpace { get; init; } = OffscreenColorSpace.Srgb;

        /// <summary>
        /// 为 true 时，每次提交渲染都要求命令列表的帧状态满足
        /// <c>ViewWidth * RenderScaleX ≈ Width</c> 且 <c>ViewHeight * RenderScaleY ≈ Height</c>，
        /// 否则该次渲染任务以 <see cref="ArgumentException"/> 失败（误差容限 0.5 像素）。
        /// 默认 false，允许调用方通过投影矩阵自行做分块/局部渲染。
        /// </summary>
        public bool RequireMatchingViewport { get; init; }

        /// <summary>校验创建参数；由后端在创建离屏上下文时调用。</summary>
        internal void Validate()
        {
            if (Width <= 0)
                throw new ArgumentOutOfRangeException(nameof(Width), Width, "离屏目标宽度必须大于 0。");
            if (Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(Height), Height, "离屏目标高度必须大于 0。");
            if (!Enum.IsDefined(PixelFormat))
                throw new ArgumentOutOfRangeException(nameof(PixelFormat), PixelFormat, "未知的像素格式。");
            if (!Enum.IsDefined(AlphaType))
                throw new ArgumentOutOfRangeException(nameof(AlphaType), AlphaType, "未知的 alpha 类型。");
            if (!Enum.IsDefined(ColorSpace))
                throw new ArgumentOutOfRangeException(nameof(ColorSpace), ColorSpace, "未知的颜色空间。");
        }

        /// <summary>按 <see cref="RequireMatchingViewport"/> 校验命令列表的帧状态；不匹配时抛 <see cref="ArgumentException"/>。</summary>
        internal void EnsureViewportMatches(in DrawCommandListFrameState frameState)
        {
            if (!RequireMatchingViewport)
                return;

            var width = frameState.ViewWidth * frameState.RenderScaleX;
            var height = frameState.ViewHeight * frameState.RenderScaleY;
            if (Math.Abs(width - Width) > 0.5f || Math.Abs(height - Height) > 0.5f)
                throw new ArgumentException($"命令列表视口({width}x{height})与离屏目标({Width}x{Height})不匹配。");
        }
    }
}
