using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;

[RegisterSingleton<ToolboxItem>]
public class KeyframeSoflanToolboxGenerator : ToolboxGenerator<KeyframeSoflan>
{
    public KeyframeSoflanToolboxGenerator() : base("Keyframe Soflan", "Soflan")
    {
    }
}
