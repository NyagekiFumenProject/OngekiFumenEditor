using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel
{
	public interface IEditorExtraContextMenuBuilder
	{
		public IEnumerable<FrameworkElement> BuildMenuItems(IEnumerable<IFumenVisualEditorExtraMenuItemHandler> registerHandlers, FumenVisualEditorViewModel targetEditor);
	}
}
