﻿using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils.Attributes;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	[MapToView(ViewType = typeof(MultiLanesOperationView))]
	public class MultiLanesOperationViewModel : PropertyChangedBase
	{
		private readonly ConnectableChildObjectBase frontChild;
		private readonly ConnectableStartObject laterStart;

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

		public void OnClick(ActionExecutionContext e)
		{
			if (IoC.Get<IFumenObjectPropertyBrowser>().Editor is not FumenVisualEditorViewModel editor)
				return;

			var frontStart = frontChild.ReferenceStartObject;
			var midChild = frontStart.CreateChildObject();

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("合并轨道", () =>
			{
				midChild.XGrid = laterStart.XGrid.CopyNew();
				midChild.TGrid = laterStart.TGrid.CopyNew();

				frontStart.AddChildObject(midChild);

				foreach (var laterChild in laterStart.Children.ToArray())
				{
					laterStart.RemoveChildObject(laterChild);
					laterChild.CacheRecoveryChildIndex = -1;
					frontStart.AddChildObject(laterChild);
				}

				editor.Fumen.RemoveObject(laterStart);
				IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
			}, () =>
			{
				var next = midChild.NextObject;
				while (next != null)
				{
					frontStart.RemoveChildObject(next);
					next.CacheRecoveryChildIndex = -1;
					laterStart.AddChildObject(next);
					next = next.NextObject;
				}
				frontStart.RemoveChildObject(midChild);
				editor.Fumen.AddObject(laterStart);

				IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
			}));
		}
	}
}
