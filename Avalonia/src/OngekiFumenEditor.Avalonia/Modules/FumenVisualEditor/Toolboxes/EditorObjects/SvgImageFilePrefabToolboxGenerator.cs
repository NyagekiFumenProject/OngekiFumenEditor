using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;

[RegisterTransient<IToolboxGenerator>]
public sealed class SvgImageFilePrefabToolboxGenerator : ToolboxGenerator<SvgImageFilePrefab>
{
}
