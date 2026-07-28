using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base
{
	public interface IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostEditor);
	}
}


