using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Svg;

internal static class PngStructureAssertions
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static PngInfo AssertValidPngEndingAtIend(byte[] data)
    {
        Assert.True(data.AsSpan().StartsWith(Signature), "PNG signature is missing.");

        var offset = Signature.Length;
        var width = 0;
        var height = 0;
        string? lastChunkType = null;
        while (offset < data.Length)
        {
            Assert.True(data.Length - offset >= 12, "PNG chunk header or CRC is truncated.");
            var dataLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)));
            var chunkEnd = checked(offset + 12 + dataLength);
            Assert.True(chunkEnd <= data.Length, "PNG chunk data is truncated.");

            var chunkType = Encoding.ASCII.GetString(data, offset + 4, 4);
            if (chunkType == "IHDR")
            {
                Assert.Equal(13, dataLength);
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4)));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 12, 4)));
            }

            lastChunkType = chunkType;
            offset = chunkEnd;
        }

        Assert.Equal("IEND", lastChunkType);
        Assert.Equal(data.Length, offset);
        Assert.True(width > 0 && height > 0, "PNG IHDR dimensions are invalid.");
        return new PngInfo(width, height);
    }

    internal sealed record PngInfo(int Width, int Height);
}
