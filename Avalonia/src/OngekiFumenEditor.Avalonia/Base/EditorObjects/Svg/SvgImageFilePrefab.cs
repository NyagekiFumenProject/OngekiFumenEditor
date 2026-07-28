using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public class SvgImageFilePrefab : SvgPrefabBase
{
    public const string CommandName = "[SVG_IMG]";
    public override string IDShortName => CommandName;

    private FileInfo svgFile;
    [ObjectPropertyBrowserSingleSelectedOnly]
    public FileInfo SvgFile
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

        if (fromObj is not SvgImageFilePrefab from)
            return;

        if (from.SvgFile?.FullName is string path)
            SvgFile = new FileInfo(path);
    }

    public void ReloadSvgFile()
    {
        if (SvgFile is null || !SvgFile.Exists)
        {
            CleanGeometry();
            return;
        }

        // SVG drawing parse/render is not migrated yet in non-UI stage.
        ApplySvgContent(SvgFile.FullName);
    }

    public override string ToString() => $"{base.ToString()} File[{Path.GetFileName(SvgFile?.Name)}]";
}

