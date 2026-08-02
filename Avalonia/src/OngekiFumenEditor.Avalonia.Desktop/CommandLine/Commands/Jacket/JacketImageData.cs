namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;

internal sealed class JacketImageData
{
    public JacketImageData(int width, int height, byte[] data)
    {
        Width = width;
        Height = height;
        Data = data;
    }

    public int Width { get; }
    public int Height { get; }
    public string Name { get; }

    /// <summary>
    /// Pure RGBA32 array
    /// </summary>
    public byte[] Data { get; }
}
