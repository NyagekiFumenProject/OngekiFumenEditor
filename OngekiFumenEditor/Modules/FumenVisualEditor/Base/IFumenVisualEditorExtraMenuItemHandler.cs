using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
	public interface IFumenVisualEditorExtraMenuItemHandler
	{
		/// <summary>
		/// e.g new []{"脚本","自定义...","打开我的脚本"}
		/// </summary>
		string[] RegisterMenuPath { get; }
		void Handle(FumenVisualEditorViewModel editor, EventArgs args);
	}
}
