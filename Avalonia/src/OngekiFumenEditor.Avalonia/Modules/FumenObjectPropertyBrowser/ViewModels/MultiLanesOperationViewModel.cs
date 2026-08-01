using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils.Attributes;
using System.Linq;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
{
	[MapToView(ViewType = typeof(MultiLanesOperationView))]
	public partial class MultiLanesOperationViewModel : ObservableObject
	{
		private readonly ConnectableChildObjectBase frontChild;
		private readonly ConnectableStartObject laterStart;

		private readonly List<ILaneDockable> RedockedObjects = new();

		/**
		 合并前:
            frontStart  frontChild
            o-----------o

                        o midChild
                            
                        o--------o---------o
                        laterStart

		合并后:
            frontStart  frontChild
            o-----------o
			            |
                        | 
                        |   
                o       o--------o---------o
       laterStart       midChild 
        */

		public MultiLanesOperationViewModel(ConnectableChildObjectBase frontChild, ConnectableStartObject laterStart)
		{
			this.frontChild = frontChild;
			this.laterStart = laterStart;
		}

		[RelayCommand]
		private void CombineLanes()
		{
			if (IoC.Get<IFumenObjectPropertyBrowser>().Editor is not FumenVisualEditorViewModel editor)
				return;

			var frontStart = frontChild.ReferenceStartObject;
			var midChild = frontStart.CreateChildObject();

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.CombineLane.ToLocalizedString(), () =>
			{
				midChild.XGrid = laterStart.XGrid.CopyNew();
				midChild.TGrid = laterStart.TGrid.CopyNew();

				frontStart.AddChildObject(midChild);

				foreach (var laterChild in laterStart.Children.ToArray())
				{
					laterStart.RemoveChildObject(laterChild);
					frontStart.AddChildObject(laterChild);
				}

				foreach (var dockable in editor.Fumen.Taps.Concat<ILaneDockable>(editor.Fumen.Holds)
					         .Where(d => d.ReferenceLaneStart == laterStart)) {
					dockable.ReferenceLaneStart = (LaneStartBase)frontStart;
					RedockedObjects.Add(dockable);
				}

				editor.Fumen.RemoveObject(laterStart);
				IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
			}, () =>
			{
				var next = midChild.NextObject;
				while (next != null)
				{
					frontStart.RemoveChildObject(next);
					laterStart.AddChildObject(next);
					next = next.NextObject;
				}
				frontStart.RemoveChildObject(midChild);
				editor.Fumen.AddObject(laterStart);

				foreach (var dockable in RedockedObjects) {
					dockable.ReferenceLaneStart = (LaneStartBase)laterStart;
				}

				IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
			}));
		}
	}
}



