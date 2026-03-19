using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.ExtraEditorMenuItems
{
	[Export(typeof(IFumenVisualEditorExtraMenuItemHandler))]
	public class PlayOrPauseMenuItem : IFumenVisualEditorExtraMenuItemHandler
	{
		public string[] RegisterMenuPath { get; } = new[] { "脚本", "AkariMindController", "播放/暂停" };

		public async void Handle(FumenVisualEditorViewModel editor, EventArgs args)
		{
			var controller = IoC.Get<IOngekiGamePlayControllerViewer>();
			//if ((await controller.GetNotesManagerData()) is not NotesManagerData data)
			//	return;
		}
	}
}
