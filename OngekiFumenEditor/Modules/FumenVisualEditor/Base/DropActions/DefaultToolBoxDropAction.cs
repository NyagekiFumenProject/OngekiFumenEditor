using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
	public class DefaultToolBoxDropAction : EditorAddObjectDropAction
	{
		private readonly Type itemType;

		public DefaultToolBoxDropAction(ToolboxItem toolboxItem)
		{
			itemType = toolboxItem.ItemType;
		}

		protected override OngekiObjectBase GetDisplayObject()
		{
			return CacheLambdaActivator.CreateInstance(itemType) switch
			{
				OngekiObjectBase o => o,
				ToolboxGenerator generator => generator.CreateDisplayObject(),
				_ => default
			};
		}
	}
}
