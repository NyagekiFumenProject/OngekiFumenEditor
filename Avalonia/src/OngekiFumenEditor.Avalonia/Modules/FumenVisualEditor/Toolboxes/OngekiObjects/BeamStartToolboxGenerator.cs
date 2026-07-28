using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects;

[RegisterTransient<IToolboxGenerator>]
public class BeamStartToolboxGenerator : ToolboxGenerator<BeamStart>
{
}
