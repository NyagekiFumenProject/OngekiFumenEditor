#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public interface ISimpleFile : IDisposable
{
    ISimpleDirectory ParentDictionary { get; }

    string FullPath { get; }

    /// <summary>
    ///     A file name such as "myFile.txt".
    /// </summary>
    string FileName { get; }

    long FileLength { get; }

    ValueTask<string[]> ReadAllLines();

    ValueTask<byte[]> ReadAllBytes();

    Task<Stream> OpenRead();
}
