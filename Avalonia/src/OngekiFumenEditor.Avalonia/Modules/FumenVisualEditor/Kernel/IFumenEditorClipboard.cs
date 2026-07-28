using Avalonia;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel
{
	public interface IFumenEditorClipboard
	{
		bool ContainPastableObjects { get; }
		IReadOnlyCollection<OngekiObjectBase> CurrentCopiedObjects { get; }

		Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption mirrorOption, Point? placePoint = default);
		Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects);
	}
}



