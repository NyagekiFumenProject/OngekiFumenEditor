using Gekimini.Avalonia.Modules.Toolbox;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
	public class WallStartToolboxGenerator<T> : ToolboxGenerator<T> where T : WallStartBase, new()
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Wall Left Start", "Ongeki Lanes")]
	public class WallLeftStartToolboxGenerator : WallStartToolboxGenerator<WallLeftStart>
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Wall Right Start", "Ongeki Lanes")]
	public class WallRightStartToolboxGenerator : WallStartToolboxGenerator<WallRightStart>
	{

	}
}


