using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
	public abstract class ToolboxGenerator
	{
		public abstract OngekiObjectBase CreateDisplayObject();
	}

	public class ToolboxGenerator<T> : ToolboxGenerator where T : OngekiObjectBase, new()
	{
		public override OngekiObjectBase CreateDisplayObject()
		{
			return new T();
		}
	}
}
