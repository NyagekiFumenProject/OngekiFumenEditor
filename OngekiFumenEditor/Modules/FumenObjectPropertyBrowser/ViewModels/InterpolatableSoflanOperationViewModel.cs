using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Properties;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class InterpolatableSoflanOperationViewModel : PropertyChangedBase
	{
		private InterpolatableSoflan soflan;

		public InterpolatableSoflanOperationViewModel(InterpolatableSoflan obj)
		{
			soflan = obj;
		}

		public void Interpolate(ActionExecutionContext e)
		{
			var list = soflan.GenerateKeyframeSoflans().OfType<OngekiObjectBase>().ToArray();
			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;

			if (editor == null)
				return;

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.InterpolateDurationSoflan, () =>
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
