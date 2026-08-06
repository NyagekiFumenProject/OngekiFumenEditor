using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;

// SVG prefabs are temporarily disabled in FumenVisualEditor.
// [RegisterSingleton<ToolboxItem>]
public sealed class SvgImageFilePrefabToolboxGenerator : ToolboxGenerator<SvgImageFilePrefab>
{
    public SvgImageFilePrefabToolboxGenerator() : base("SvgPrefab(File)", "Misc")
    {
    }
}
