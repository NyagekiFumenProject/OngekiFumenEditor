namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

internal interface ISvgRasterizer
{
    Task RasterizeAsync(ReadOnlyMemory<byte> svgData, string outputFilePath, CancellationToken cancellationToken);
}
