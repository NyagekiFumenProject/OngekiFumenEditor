using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class AudioStreamFormatDetectorTests
{
    [Fact]
    public async Task DetectAsync_UsesBytesReadFromAsyncOnlyStream()
    {
        byte[] header = "RIFF"u8.ToArray();
        Array.Resize(ref header, 12);
        "WAVE"u8.CopyTo(header.AsSpan(8));
        await using var stream = new SeekableStream(
            new AsyncOnlyReadStream(header),
            header.LongLength);

        Assert.Equal(AudioStreamFormat.Wav, await AudioStreamFormatDetector.DetectAsync(stream));
    }

    [Fact]
    public async Task DetectAsync_RestoresPositionAfterReadingHeader()
    {
        byte[] header = new byte[15];
        "RIFF"u8.CopyTo(header.AsSpan(3));
        "WAVE"u8.CopyTo(header.AsSpan(11));
        await using var stream = new MemoryStream(header);
        stream.Position = 3;

        Assert.Equal(AudioStreamFormat.Wav, await AudioStreamFormatDetector.DetectAsync(stream));
        Assert.Equal(3, stream.Position);
    }

    private sealed class AsyncOnlyReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Browser supports only ReadAsync");

        public override int Read(Span<byte> buffer) =>
            throw new NotSupportedException("Browser supports only ReadAsync");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
