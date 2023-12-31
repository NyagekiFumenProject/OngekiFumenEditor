using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;

namespace OngekiFumenEditor.Kernel.SettingPages.Program.ViewModels
{
	[Export(typeof(ISettingsEditor))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class ProgramSettingViewModel : PropertyChangedBase, ISettingsEditor
	{
		public Properties.ProgramSetting Setting => Properties.ProgramSetting.Default;

		public ProgramSettingViewModel()
		{
			Setting.PropertyChanged += SettingPropertyChanged;
		}

		private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Log.LogDebug($"logs setting property changed : {e.PropertyName}");
		}

		public string SettingsPageName => Resource.TabProgram;

		public string SettingsPagePath => Resource.TabEnviorment;

		public void ApplyChanges()
		{
			Setting.Save();
		}

		public void OnDumpFolderPathButtonClick()
		{
			using var openFolderDialog = new FolderBrowserDialog();
			openFolderDialog.ShowNewFolderButton = true;
			openFolderDialog.SelectedPath = Path.GetFullPath(Setting.DumpFileDirPath);
			if (openFolderDialog.ShowDialog() == DialogResult.OK)
			{
				var folderPath = openFolderDialog.SelectedPath;
				if (!Directory.Exists(folderPath))
				{
					MessageBox.Show(Resource.ErrorFolderIsEmpty);
					OnDumpFolderPathButtonClick();
					return;
				}
				Setting.DumpFileDirPath = folderPath;
				ApplyChanges();
			}
		}

		public void ThrowException()
		{
			throw new Exception("塔塔开!");
		}
	}
}
