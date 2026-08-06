#nullable enable

#if ENABLE_SVG_PREFAB_OBJECTS
using System.Security.Cryptography;
using System.Text;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Svg.Skia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;

public static class SvgProjectFileImporter
{
    private const string ImportRoot = "autoImport";
    private const string SvgFolder = "svgFiles";

    public static async Task<ISimpleFile> ImportAsync(
        ISimpleDirectory projectRoot,
        ISimpleFile sourceFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);
        ArgumentNullException.ThrowIfNull(sourceFile);

        using var ioLease = await EditorProjectIoGate.EnterAsync(cancellationToken);
        var content = await sourceFile.ReadAllBytes();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSvg(content);

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var importRoot = await projectRoot.GetOrCreateDirectoryAsync(ImportRoot, cancellationToken);
        var svgFolder = await importRoot.GetOrCreateDirectoryAsync(SvgFolder, cancellationToken);
        var hashSuffix = $".{hash}.svg";

        foreach (var existing in svgFolder.ChildFiles.Where(x =>
                     x.FileName.EndsWith(hashSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            var existingContent = await existing.ReadAllBytes();
            if (!content.AsSpan().SequenceEqual(existingContent))
                throw new InvalidDataException($"Imported SVG hash entry '{existing.FileName}' has unexpected content.");

            ValidateSvg(existingContent);
            return new ProjectResourceSimpleFile(
                existing,
                EditorProjectPathResolver.GetRootRelativeLocator(existing),
                existingContent);
        }

        var targetName = $"{SanitizeStem(sourceFile.FileName)}.{hash}.svg";
        ISimpleFile? createdFile = null;
        try
        {
            createdFile = await svgFolder.CreateFileAsync(targetName, cancellationToken);
            await createdFile.WriteAsync(
                (stream, writerCancellationToken) =>
                    stream.WriteAsync(content, writerCancellationToken).AsTask(),
                cancellationToken);

            var verifiedContent = await createdFile.ReadAllBytes();
            if (!content.AsSpan().SequenceEqual(verifiedContent))
                throw new IOException($"Imported SVG '{targetName}' failed content verification.");
            ValidateSvg(verifiedContent);

            return new ProjectResourceSimpleFile(
                createdFile,
                EditorProjectPathResolver.GetRootRelativeLocator(createdFile),
                verifiedContent);
        }
        catch
        {
            if (createdFile is not null)
            {
                try
                {
                    await createdFile.DeleteAsync(CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    Log.LogWarn($"Unable to remove incomplete imported SVG '{targetName}': {cleanupException.Message}");
                }
            }

            throw;
        }
    }

    private static string SanitizeStem(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var builder = new StringBuilder(Math.Min(stem.Length, 80));
        foreach (var character in stem.Take(80))
        {
            builder.Append(char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                ? '_'
                : character);
        }

        var result = builder.ToString().Trim(' ', '.');
        return string.IsNullOrWhiteSpace(result) ? "svg" : result;
    }

    private static void ValidateSvg(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var svg = new SKSvg();
        try
        {
            if (svg.Load(stream) is null)
                throw new InvalidDataException("The selected file is not a valid SVG image.");
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException("The selected file is not a valid SVG image.", exception);
        }
    }
}
#endif
