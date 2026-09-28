using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
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
        public Properties.DefaultWaveformSettings WaveformSetting => Properties.DefaultWaveformSettings.Default;

        public IEnumerable<AudioOutputType> AudioOutputTypeValues => Enum.GetValues<AudioOutputType>().OrderBy(x => x);

        /// <summary>
        /// 波形颜色行（逐个包装 <see cref="DefaultWaveformSettings"/> 中以 Waveform 开头、类型为
        /// <see cref="System.Drawing.Color"/> 的属性），供设置页显示与调色。
        /// </summary>
        public ColorPropertyWrapper[] WaveformColorsProperties { get; }

        public AudioSettingViewModel()
        {
            Setting.PropertyChanged += SettingPropertyChanged;

            WaveformColorsProperties = typeof(DefaultWaveformSettings)
                .GetProperties()
                .Where(x => x.Name.StartsWith("Waveform") && x.PropertyType == typeof(System.Drawing.Color))
                .Select(x => new ColorPropertyWrapper(x, DefaultWaveformSettings.Default))
                .ToArray();
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
            WaveformSetting.Save();
        }

        public void OnSelectWaveformColor(ActionExecutionContext context)
        {
            if (context.Source.DataContext is not ColorPropertyWrapper colorProperty)
                return;

            var dialog = new CommonColorPicker(() =>
            {
                return colorProperty.Color.ToMediaColor();
            }, color =>
            {
                colorProperty.Color = color.ToDrawingColor();
            }, Resources.NamedColorChangeTitle.Format(colorProperty.Name));
            dialog.Show();
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
