using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Toolbox;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Avalonia;
using Gekimini.Avalonia.Utils.MethodExtensions;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public partial class InterpolatableSoflanOperationViewModel : ViewModelBase
	{
		private InterpolatableSoflan soflan;

		public InterpolatableSoflanOperationViewModel(InterpolatableSoflan obj)
		{
			soflan = obj;
		}

		[RelayCommand]
		private void Interpolate()
		{
			var list = soflan.GenerateKeyframeSoflans().OfType<OngekiObjectBase>().ToArray();
			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;

			if (editor == null)
				return;

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.InterpolateDurationSoflan.ToLocalizedString(), () =>
			{
				editor.Fumen.AddObjects(list);
				editor.Fumen.RemoveObject(soflan);
			}, () =>
			{
				editor.Fumen.AddObject(soflan);
				editor.Fumen.RemoveObjects(list);
			}));
		}
	}
}



