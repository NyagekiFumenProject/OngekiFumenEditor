using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
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
