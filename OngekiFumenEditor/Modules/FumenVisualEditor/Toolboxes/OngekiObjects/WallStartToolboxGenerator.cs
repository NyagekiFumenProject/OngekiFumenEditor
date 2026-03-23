using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
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
