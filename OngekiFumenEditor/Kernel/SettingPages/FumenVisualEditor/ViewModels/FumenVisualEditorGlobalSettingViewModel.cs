using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.ViewModels
{
	[Export(typeof(ISettingsEditor))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class FumenVisualEditorGlobalSettingViewModel : PropertyChangedBase, ISettingsEditor
	{
		public Properties.EditorGlobalSetting Setting => Properties.EditorGlobalSetting.Default;

		public FumenVisualEditorGlobalSettingViewModel()
		{
			Properties.EditorGlobalSetting.Default.PropertyChanged += SettingPropertyChanged;
		}

		private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Log.LogDebug($"editor global setting property changed : {e.PropertyName}");
		}

		public string SettingsPageName => "可视编辑器";

		public string SettingsPagePath => "文档";

		public void ApplyChanges()
		{
			Properties.EditorGlobalSetting.Default.Save();
		}

		public void ClearRecentOpen()
		{
			IoC.Get<IEditorRecentFilesManager>().ClearAllRecords();
		}
	}
}
