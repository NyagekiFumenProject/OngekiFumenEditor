using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;

namespace OngekiFumenEditor.Kernel.SettingPages.Logs.ViewModels
{
	[Export(typeof(ISettingsEditor))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class LogsSettingViewModel : PropertyChangedBase, ISettingsEditor
	{
		public Properties.LogSetting Setting => Properties.LogSetting.Default;

		public LogsSettingViewModel()
		{
			Setting.PropertyChanged += SettingPropertyChanged;
		}

		private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Log.LogDebug($"logs setting property changed : {e.PropertyName}");
		}

		public string SettingsPageName => Resources.TabLogger;

		public string SettingsPagePath => Resources.TabEnviorment;

		public void ApplyChanges()
		{
			Setting.Save();
		}

		public void OnLogsFolderPathButtonClick()
		{
			using var openFolderDialog = new FolderBrowserDialog();
			openFolderDialog.ShowNewFolderButton = true;
			openFolderDialog.SelectedPath = Path.GetFullPath(Setting.LogFileDirPath);
			if (openFolderDialog.ShowDialog() == DialogResult.OK)
			{
				var folderPath = openFolderDialog.SelectedPath;
				if (!Directory.Exists(folderPath))
				{
					MessageBox.Show(Resources.ErrorFolderIsEmpty);
					OnLogsFolderPathButtonClick();
					return;
				}
				Setting.LogFileDirPath = folderPath;
				ApplyChanges();
			}
		}
	}
}
