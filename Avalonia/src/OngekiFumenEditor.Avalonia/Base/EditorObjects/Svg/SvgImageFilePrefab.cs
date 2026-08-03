#nullable enable

using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public sealed class SvgImageFilePrefab : SvgPrefabBase
{
    public const string CommandName = "[SVG_IMG]";
    private ISimpleFile? svgFile;

    public override string IDShortName => CommandName;

    [ObjectPropertyBrowserSingleSelectedOnly]
    public ISimpleFile? SvgFile
    {
        get => svgFile;
        set
        {
            if (ReferenceEquals(svgFile, value))
                return;

            svgFile?.Dispose();
            if (SetProperty(ref svgFile, value))
                ReloadSvgFile();
        }
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgImageFilePrefab from)
            return;

        if (from.SvgFile is null)
        {
            SvgFile = null;
        }
        else if (!string.IsNullOrWhiteSpace(from.SvgFile.LocalPath) &&
                 !File.Exists(from.SvgFile.LocalPath))
        {
            SvgFile = new LocalSimpleFile(from.SvgFile.LocalPath);
        }
        else
        {
            var bytes = from.SvgFile.ReadAllBytes().AsTask().GetAwaiter().GetResult();
            SvgFile = new MemorySimpleFile(
                from.SvgFile.FileName,
                from.SvgFile.FullPath,
                bytes.ToArray(),
                from.SvgFile.LocalPath);
        }
    }

    public void ReloadSvgFile()
    {
        CleanGeometry();
        if (SvgFile is null ||
            (!string.IsNullOrWhiteSpace(SvgFile.LocalPath) && !File.Exists(SvgFile.LocalPath)))
            return;

        using var stream = SvgFile.OpenRead().GetAwaiter().GetResult();
        ApplySvgContent(stream);
    }

    public override void Dispose()
    {
        svgFile?.Dispose();
        svgFile = null;
        base.Dispose();
    }

    public override string ToString() => $"{base.ToString()} File[{SvgFile?.FileName}]";
}
