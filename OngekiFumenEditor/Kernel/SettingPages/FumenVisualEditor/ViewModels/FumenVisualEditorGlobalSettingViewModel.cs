using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using Xceed.Wpf.Toolkit;

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

        public string SettingsPageName => Resources.TabEditor;

        public string SettingsPagePath => Resources.TabDocument;

        public void ApplyChanges()
        {
            Properties.EditorGlobalSetting.Default.Save();
        }

        public void ClearRecentOpen()
        {
            IoC.Get<IEditorRecentFilesManager>().ClearAllRecords();
        }

        public void OnSelectForegroundColor(ActionExecutionContext context)
        {
            var dialog = new CommonColorPicker(() =>
            {
                return Setting.PlayFieldForegroundColor.AsARGBToColor().ToMediaColor();
            }, color =>
            {
                Setting.PlayFieldForegroundColor = color.ToDrawingColor().ToArgb();
            }, "设置颜色");
            dialog.Show();
        }

        public void OnSelectBackgroundColor(ActionExecutionContext context)
        {
            var dialog = new CommonColorPicker(() =>
            {
                return Setting.PlayFieldBackgroundColor.AsARGBToColor().ToMediaColor();
            }, color =>
            {
                Setting.PlayFieldBackgroundColor = color.ToDrawingColor().ToArgb();
            }, "设置颜色");
            dialog.Show();
        }
    }
}
