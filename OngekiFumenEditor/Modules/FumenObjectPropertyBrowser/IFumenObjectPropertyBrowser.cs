using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser
{
	public interface IFumenObjectPropertyBrowser : ITool
	{
		public IReadOnlySet<ISelectableObject> SelectedObjects { get; }
		public FumenVisualEditorViewModel Editor { get; }

		public void RefreshSelected(FumenVisualEditorViewModel referenceEditor);
		public void RefreshSelected(FumenVisualEditorViewModel referenceEditor, params object[] ongekiObj);
	}
}
