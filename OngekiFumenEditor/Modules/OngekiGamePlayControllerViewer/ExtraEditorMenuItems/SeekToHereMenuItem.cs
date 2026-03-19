using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.ExtraEditorMenuItems
{
	[Export(typeof(IFumenVisualEditorExtraMenuItemHandler))]
	public class SeekToHereMenuItem : IFumenVisualEditorExtraMenuItemHandler
	{
		public string[] RegisterMenuPath { get; } = new[] { "脚本", "AkariMindController", "控制游戏跳转到这里" };

		public async void Handle(FumenVisualEditorViewModel editor, EventArgs args)
		{
			var controller = IoC.Get<IOngekiGamePlayControllerViewer>();
			var curTGrid = editor.GetCurrentTGrid();
			var msec = TGridCalculator.ConvertTGridToAudioTime(curTGrid, editor);

			await controller.SeekTo(msec);
		}
	}
}
