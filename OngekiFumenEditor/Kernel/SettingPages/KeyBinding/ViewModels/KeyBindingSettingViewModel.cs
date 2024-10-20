using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Models;
using OngekiFumenEditor.Kernel.SettingPages.KeyBinding.Dialogs;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.SettingPages.KeyBinding.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class KeyBindingSettingViewModel : PropertyChangedBase, ISettingsEditor
    {
        private readonly IKeyBindingManager keybindingManager;

        public KeyBindingSettingViewModel()
        {
            keybindingManager = IoC.Get<IKeyBindingManager>();

            Definitions = keybindingManager.KeyBindingDefinations.ToArray();
        }

        public string SettingsPagePath => "快捷键";

        public string SettingsPageName => "键位设置";

        public KeyBindingDefinition[] Definitions { get; }

        public void ApplyChanges()
        {
            keybindingManager.SaveConfig();
        }

        public void ChangeKeybind(ActionExecutionContext ctx)
        {
            if (ctx.Source.DataContext is not KeyBindingDefinition definition)
                return;

            var dialog = new ConfigKeyBindingDialog(definition);
            if (dialog.ShowDialog() is true)
            {
                if (KeyBindingDefinition.TryParseExpression(dialog.CurrentExpression, out var newKey, out var newModifier))
                    keybindingManager.ChangeKeyBinding(definition, newKey, newModifier);
            }
        }
    }
}
