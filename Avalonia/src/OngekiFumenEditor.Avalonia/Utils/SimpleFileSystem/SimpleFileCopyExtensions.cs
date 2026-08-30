#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public static class SimpleFileCopyExtensions
{
    private const int StreamBufferLength = 81_920;

    /// <summary>
    ///     Streams the whole content of <paramref name="source"/> into <paramref name="target"/>
    ///     through the target's transactional <see cref="ISimpleFile.WriteAsync"/> commit. The
    ///     previous target content stays intact when the copy fails partway.
    /// </summary>
    public static async Task CopyContentToAsync(
        this ISimpleFile source,
        ISimpleFile target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        await target.WriteAsync(
            async (output, writerCancellationToken) =>
            {
                await using var input = await source
                    .OpenReadAsync(writerCancellationToken)
                    ;
                await input.CopyToAsync(
                    output,
                    StreamBufferLength,
                    writerCancellationToken);
            },
            cancellationToken);
    }
}
