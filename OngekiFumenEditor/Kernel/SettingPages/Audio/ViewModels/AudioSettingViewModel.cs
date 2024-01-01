using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OngekiFumenEditor.Kernel.SettingPages.Audio.ViewModels
{
	[Export(typeof(ISettingsEditor))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class AudioSettingViewModel : PropertyChangedBase, ISettingsEditor
	{
		public Properties.AudioSetting Setting => Properties.AudioSetting.Default;
		public Properties.AudioPlayerToolViewerSetting PlayerSetting => Properties.AudioPlayerToolViewerSetting.Default;

		public IEnumerable<AudioOutputType> AudioOutputTypeValues => Enum.GetValues<AudioOutputType>().OrderBy(x => x);

		public AudioSettingViewModel()
		{
			Setting.PropertyChanged += SettingPropertyChanged;
		}

		private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Log.LogDebug($"audio setting property changed : {e.PropertyName}");
		}

		public string SettingsPageName => Resources.TabAudio;

		public string SettingsPagePath => Resources.TabSound;

		public void ApplyChanges()
		{
			Setting.Save();
			PlayerSetting.Save();
		}

		public void OnSoundFolderPathButtonClick()
		{
			using var openFolderDialog = new FolderBrowserDialog();
			openFolderDialog.ShowNewFolderButton = true;
			openFolderDialog.SelectedPath = Path.GetFullPath(Setting.SoundFolderPath);
			if (openFolderDialog.ShowDialog() == DialogResult.OK)
			{
				var folderPath = openFolderDialog.SelectedPath;
				if (!Directory.Exists(folderPath))
				{
					MessageBox.Show(Resources.ErrorSoundFolderIsEmptyFile);
					OnSoundFolderPathButtonClick();
					return;
				}
				Setting.SoundFolderPath = folderPath;
				ApplyChanges();
			}
		}
	}
}
