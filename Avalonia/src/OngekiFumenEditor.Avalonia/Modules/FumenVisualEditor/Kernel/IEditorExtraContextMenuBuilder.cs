using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel
{
	public interface IEditorExtraContextMenuBuilder
	{
		public IEnumerable<FrameworkElement> BuildMenuItems(IEnumerable<IFumenVisualEditorExtraMenuItemHandler> registerHandlers, FumenVisualEditorViewModel targetEditor);
	}
}

