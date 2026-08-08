using System.Text;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorProjectLoadOwnershipTests
{
    [AvaloniaFact]
    public async Task FailedLoad_DisposesOwnedRootAndChildren()
    {
        await using var projectBuffer = new MemoryStream();
        await new EditorProjectFileManager().Save(
            projectBuffer,
            new EditorProjectDataModel
            {
                FumenFilePath = "missing.ogkr",
                AudioFilePath = "missing.wav"
            });

        var projectFile = new TrackingFile(
            "project.nyagekiProj",
            projectBuffer.ToArray());
        var root = new TrackingDirectory(projectFile);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EditorProjectDataUtils.TryLoadFromFileAsync(
                root,
                projectFile,
                "project.nyagekiProj"));

        Assert.Equal(1, root.DisposeCount);
        Assert.Equal(1, projectFile.DisposeCount);
    }

    private sealed class TrackingDirectory(TrackingFile projectFile) : ISimpleDirectory
    {
        private bool isDisposed;

        public int DisposeCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => null;
        public ISimpleDirectory[] ChildDictionaries => [];
        public ISimpleFile[] ChildFiles => [projectFile];
        public string FullPath => "memory://project";
        public string? LocalPath => null;
        public string DirectoryName => string.Empty;

        public bool ExistsDirectory(string dirName) => false;
        public bool ExistsFile(string fileName) =>
            fileName.Equals(projectFile.FileName, StringComparison.OrdinalIgnoreCase);
        public ISimpleFile[] GetFiles(string pattern = "*") => ChildFiles;

        public Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
            string directoryName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ISimpleFile> CreateFileAsync(
            string fileName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            DisposeCount++;
            projectFile.Dispose();
        }
    }

    private sealed class TrackingFile : ISimpleFile
    {
        private readonly byte[] content;
        private bool isDisposed;

        public TrackingFile(string fileName, byte[] content)
        {
            FileName = fileName;
            this.content = content;
        }

        public int DisposeCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"memory://project/{FileName}";
        public string? LocalPath => null;
        public string FileName { get; }
        public long FileLength => content.LongLength;

        public ValueTask<string[]> ReadAllLines()
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(
                Encoding.UTF8.GetString(content).Split(["\r\n", "\n"], StringSplitOptions.None));
        }

        public ValueTask<byte[]> ReadAllBytes()
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(content.ToArray());
        }

        public Task<Stream> OpenRead()
        {
            ThrowIfDisposed();
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task<Stream> OpenWrite() => throw new NotSupportedException();

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            DisposeCount++;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
        }
    }
}
