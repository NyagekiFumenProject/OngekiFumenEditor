#nullable enable

using OngekiFumenEditor.Avalonia.Base.Attributes;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public sealed class SvgImageFilePrefab : SvgPrefabBase
{
    public const string CommandName = "[SVG_IMG]";
    private FileInfo? svgFile;

    public override string IDShortName => CommandName;

    [ObjectPropertyBrowserSingleSelectedOnly]
    public FileInfo? SvgFile
    {
        get => svgFile;
        set
        {
            if (SetProperty(ref svgFile, value))
                ReloadSvgFile();
        }
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is SvgImageFilePrefab from)
            SvgFile = from.SvgFile is null ? null : new FileInfo(from.SvgFile.FullName);
    }

    public void ReloadSvgFile()
    {
        CleanGeometry();
        if (SvgFile is null || !SvgFile.Exists)
            return;

        using var stream = SvgFile.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        ApplySvgContent(stream);
    }

    public override string ToString() => $"{base.ToString()} File[{SvgFile?.Name}]";
}
