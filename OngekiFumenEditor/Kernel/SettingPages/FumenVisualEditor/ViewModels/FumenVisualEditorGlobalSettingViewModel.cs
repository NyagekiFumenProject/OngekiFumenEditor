using Caliburn.Micro;
using Gemini.Framework.Languages;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.Globalization;
using Xceed.Wpf.Toolkit;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class FumenVisualEditorGlobalSettingViewModel : PropertyChangedBase, ISettingsEditor
    {
        private const int HoldBodyWidthMin = 1;
        private const int HoldBodyWidthMax = 50;

        public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

        private string holdBodyWidthText;
        public string HoldBodyWidthText
        {
            get => holdBodyWidthText;
            set => Set(ref holdBodyWidthText, value);
        }

        private int holdBodyWidthBeforeEdit;
        private bool isEditing;

        public FumenVisualEditorGlobalSettingViewModel()
        {
            EditorGlobalSetting.Default.PropertyChanged += SettingPropertyChanged;
            RefreshHoldBodyWidthText(NormalizeHoldBodyWidth(Setting.HoldBodyWidth));
        }

        private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Log.LogDebug($"editor global setting property changed : {e.PropertyName}");
            if (e.PropertyName == nameof(EditorGlobalSetting.HoldBodyWidth))
            {
                var normalized = NormalizeHoldBodyWidth(Setting.HoldBodyWidth);
                if (Setting.HoldBodyWidth != normalized)
                {
                    Setting.HoldBodyWidth = normalized;
                    return;
                }

                RefreshHoldBodyWidthText(normalized);
            }
        }

        public string SettingsPageName => Resources.TabEditor;

        public string SettingsPagePath => Resources.TabDocument;

        public void ApplyChanges()
        {
            CommitHoldBodyWidth();
            EditorGlobalSetting.Default.Save();
        }

        public void BeginEdit()
        {
            holdBodyWidthBeforeEdit = NormalizeHoldBodyWidth(Setting.HoldBodyWidth);
            isEditing = true;
            SetHoldBodyWidth(holdBodyWidthBeforeEdit);
        }

        public void CancelChanges()
        {
            if (!isEditing)
                return;

            isEditing = false;
            SetHoldBodyWidth(holdBodyWidthBeforeEdit);
        }

        public void CommitHoldBodyWidth()
        {
            if (!int.TryParse(HoldBodyWidthText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
            {
                RefreshHoldBodyWidthText(NormalizeHoldBodyWidth(Setting.HoldBodyWidth));
                return;
            }

            SetHoldBodyWidth(NormalizeHoldBodyWidth(value));
        }

        private static int NormalizeHoldBodyWidth(int value)
            => Math.Clamp(value, HoldBodyWidthMin, HoldBodyWidthMax);

        private void SetHoldBodyWidth(int value)
        {
            if (Setting.HoldBodyWidth != value)
                Setting.HoldBodyWidth = value;

            RefreshHoldBodyWidthText(value);
        }

        private void RefreshHoldBodyWidthText(int value)
        {
            HoldBodyWidthText = value.ToString(CultureInfo.CurrentCulture);
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
            }, Resources.ChangeColor);
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
            }, Resources.ChangeColor);
            dialog.Show();
        }
    }
}
