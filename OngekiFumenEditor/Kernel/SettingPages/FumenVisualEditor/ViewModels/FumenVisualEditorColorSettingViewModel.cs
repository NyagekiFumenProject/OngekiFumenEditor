using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class FumenVisualEditorColorSettingViewModel : PropertyChangedBase, ISettingsEditor
    {
        public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

        public FumenVisualEditorColorSettingViewModel()
        {
            ColorsProperties = typeof(EditorGlobalSetting)
                .GetProperties()
                .Where(x => x.Name.StartsWith("Color") && x.PropertyType == typeof(System.Drawing.Color))
                .Select(x => new ColorPropertyWrapper(x, EditorGlobalSetting.Default))
                .ToArray();
        }

        public string SettingsPagePath => Resources.TabDocument + "\\" + Resources.TabEditor;

        public string SettingsPageName => Resources.VisualEditorLaneColorSettings;

        public ColorPropertyWrapper[] ColorsProperties { get; private set; }

        public void ApplyChanges()
        {
            EditorGlobalSetting.Default.Save();
        }

        public void OnSelectColor(ActionExecutionContext context)
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
    }
}
