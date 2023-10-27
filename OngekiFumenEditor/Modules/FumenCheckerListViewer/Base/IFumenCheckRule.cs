using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
	public interface IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostEditor);
	}
}
