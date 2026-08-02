using Injectio.Attributes;
using SkiaSharp;
using Svg.Skia;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

[RegisterSingleton<ISvgRasterizer>]
internal sealed class DefaultSvgRasterizer : ISvgRasterizer
{
    public async Task RasterizeAsync(
        ReadOnlyMemory<byte> svgData,
        string outputFilePath,
        CancellationToken cancellationToken)
    {
        var (width, height) = ReadDeclaredDimensions(svgData);
        using var svg = new SKSvg();
        var picture = svg.FromSvg(Encoding.UTF8.GetString(svgData.Span))
            ?? throw new InvalidDataException("SVG data could not be rendered.");

        var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("PNG rendering surface could not be created.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidDataException("SVG picture has invalid dimensions.");

        canvas.Scale(width / bounds.Width, height / bounds.Height);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encoding failed.");
        await File.WriteAllBytesAsync(outputFilePath, encoded.ToArray(), cancellationToken);
    }

    private static (int Width, int Height) ReadDeclaredDimensions(ReadOnlyMemory<byte> svgData)
    {
        using var stream = new MemoryStream(svgData.ToArray(), writable: false);
        var document = XDocument.Load(stream, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("SVG document has no root element.");
        var width = ParseDimension(root.Attribute("width")?.Value, "width");
        var height = ParseDimension(root.Attribute("height")?.Value, "height");
        return (width, height);
    }

    private static int ParseDimension(string value, string attributeName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dimension) ||
            !double.IsFinite(dimension) || dimension <= 0 || dimension > int.MaxValue)
        {
            throw new InvalidDataException($"SVG {attributeName} is invalid.");
        }

        return checked((int)Math.Ceiling(dimension));
    }
}
