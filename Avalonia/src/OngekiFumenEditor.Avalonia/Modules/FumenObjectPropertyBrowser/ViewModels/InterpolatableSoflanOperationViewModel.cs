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
using OngekiFumenEditor.Avalonia;
using Gekimini.Avalonia.Utils.MethodExtensions;
using OngekiFumenEditor.Avalonia.Utils;

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
			Log.LogInfo("Interpolate triggered (interpolatable soflan).");
			var list = soflan.GenerateKeyframeSoflans().OfType<OngekiObjectBase>().ToArray();
			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;

			if (editor == null)
				return;

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.InterpolateDurationSoflan.ToLocalizedString(), () =>
			{
				editor.EditorContext.Fumen.AddObjects(list);
				editor.EditorContext.Fumen.RemoveObject(soflan);
			}, () =>
			{
				editor.EditorContext.Fumen.AddObject(soflan);
				editor.EditorContext.Fumen.RemoveObjects(list);
			}));
		}
	}
}



