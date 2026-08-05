using Gekimini.Avalonia.Modules.Toolbox.Models;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions
{
	public class DefaultToolBoxDropAction : EditorAddObjectDropAction
	{
		private readonly ToolboxItem toolboxItem;

		public DefaultToolBoxDropAction(ToolboxItem toolboxItem)
		{
			this.toolboxItem = toolboxItem;
		}

		protected override OngekiObjectBase GetDisplayObject()
		{
			if (toolboxItem is IToolboxGenerator generator)
				return generator.CreateDisplayObject();

			if (string.IsNullOrWhiteSpace(toolboxItem.ItemType))
				return default;

			var itemType = Type.GetType(toolboxItem.ItemType);
			if (itemType is null)
				return default;

			return CacheLambdaActivator.CreateInstance(itemType) switch
			{
				OngekiObjectBase o => o,
				IToolboxGenerator fallbackGenerator => fallbackGenerator.CreateDisplayObject(),
				_ => default
			};
		}
	}
}


