using System.Text;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Parser;

public sealed class AsyncOnlyReadStreamParserTests
{
    [Fact]
    public async Task OgkrParser_ReadsAsyncOnlyStreamWithoutSynchronousProbe()
    {
        var content = Encoding.UTF8.GetBytes("unknown 1\n");
        await using var stream = CreateSeekableStream(content);

        var fumen = await new DefaultOngekiFumenParser([]).DeserializeAsync(stream);

        Assert.Empty(fumen.Taps);
    }

    [Fact]
    public async Task NyagekiParser_ReadsAsyncOnlyStreamWithoutSynchronousProbe()
    {
        var content = Encoding.UTF8.GetBytes("unknown:value\n");
        await using var stream = CreateSeekableStream(content);

        var fumen = await new DefaultNyagekiFumenParser([]).DeserializeAsync(stream);

        Assert.Empty(fumen.Taps);
    }

    private static SeekableStream CreateSeekableStream(byte[] content)
    {
        return new SeekableStream(new AsyncOnlyReadStream(content), content.LongLength);
    }

    private sealed class AsyncOnlyReadStream : Stream
    {
        private readonly MemoryStream inner;

        public AsyncOnlyReadStream(byte[] content)
        {
            inner = new MemoryStream(content, writable: false);
        }

        public override bool CanRead => inner.CanRead;

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Browser supports only ReadAsync");
        }

        public override int Read(Span<byte> buffer)
        {
            throw new NotSupportedException("Browser supports only ReadAsync");
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
