using Gekimini.Avalonia.Modules.Toolbox.Models;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions
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


