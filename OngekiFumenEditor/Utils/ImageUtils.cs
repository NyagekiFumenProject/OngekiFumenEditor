using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Utils
{
    /// <summary>
    /// 基于 SkiaSharp 的基础图片操作：解码、拉伸到目标尺寸、垂直翻转、PNG 编码。
    /// 像素统一为未预乘的 RGBA8888（与 Unity 纹理数据的字节序一致）。
    /// </summary>
    public static class ImageUtils
    {
        public readonly record struct RgbaImage(int Width, int Height, byte[] Data);

        /// <summary>
        /// 解码图片为未预乘 RGBA32；指定目标宽高时按拉伸（Stretch）缩放。
        /// </summary>
        public static RgbaImage LoadRgba32(string filePath, int? targetWidth = null, int? targetHeight = null)
        {
            if (targetWidth.HasValue != targetHeight.HasValue)
                throw new ArgumentException("targetWidth 与 targetHeight 必须同时指定或同时省略。");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("图片文件不存在。", filePath);

            using var source = SKBitmap.Decode(filePath);
            if (source is null)
                throw new InvalidDataException($"无法解码图片：{filePath}");

            var width = targetWidth ?? source.Width;
            var height = targetHeight ?? source.Height;
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetWidth), "目标尺寸必须为正数。");

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var target = new SKBitmap(info);
            if (!source.ScalePixels(target, new SKSamplingOptions(SKCubicResampler.Mitchell)))
                throw new InvalidDataException($"无法缩放图片：{filePath}");

            var data = new byte[width * height * 4];
            var rowBytes = width * 4;
            if (target.RowBytes == rowBytes)
            {
                target.GetPixelSpan().CopyTo(data);
            }
            else
            {
                for (var y = 0; y < height; y++)
                    Marshal.Copy(target.GetAddress(0, y), data, y * rowBytes, rowBytes);
            }
            return new RgbaImage(width, height, data);
        }

        /// <summary>
        /// 原地垂直翻转（按行交换）。
        /// </summary>
        public static void FlipVertical(byte[] rgba, int width, int height)
        {
            if (rgba is null) throw new ArgumentNullException(nameof(rgba));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            var rowBytes = width * 4;
            if (rgba.Length != rowBytes * height)
                throw new ArgumentException("RGBA 缓冲长度与宽高不匹配。", nameof(rgba));

            var row = new byte[rowBytes];
            for (var y = 0; y < height / 2; y++)
            {
                var top = y * rowBytes;
                var bottom = (height - 1 - y) * rowBytes;
                rgba.AsSpan(top, rowBytes).CopyTo(row);
                rgba.AsSpan(bottom, rowBytes).CopyTo(rgba.AsSpan(top, rowBytes));
                row.AsSpan().CopyTo(rgba.AsSpan(bottom, rowBytes));
            }
        }

        /// <summary>
        /// 把未预乘 RGBA32 像素编码为 PNG（保留 alpha）。
        /// </summary>
        public static byte[] EncodePng(ReadOnlySpan<byte> rgba, int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            var rowBytes = width * 4;
            if (rgba.Length != rowBytes * height)
                throw new ArgumentException("RGBA 缓冲长度与宽高不匹配。", nameof(rgba));

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap();
            unsafe
            {
                fixed (byte* pixels = rgba)
                {
                    if (!bitmap.InstallPixels(info, (IntPtr)pixels, rowBytes))
                        throw new InvalidOperationException("无法创建 PNG 编码位图。");
                    using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                    if (encoded is null)
                        throw new InvalidOperationException("PNG 编码失败。");
                    return encoded.ToArray();
                }
            }
        }
    }
}
