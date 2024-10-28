using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Kernel.SettingPages.FumenVisualEditor.Models;
using OngekiFumenEditor.Kernel.SettingPages.KeyBinding.Dialogs;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;

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

            definitions = keybindingManager.KeyBindingDefinations.OrderBy(x => x.DisplayName).ToArray();
            UpdateDisplayList();
        }

        public void UpdateDisplayList()
        {
            Definitions.Clear();
            var list = definitions.AsEnumerable();

            if (IsShowNotAssignOnly)
                list = list.Where(x => x.Key == Key.None);

            if (!string.IsNullOrWhiteSpace(FilterKeywords))
                list = list.Where(x => string.Join(" ", [
                    x.Name,
                    x.Key,
                    x.Modifiers,
                    x.ConfigKey
                ]).Contains(FilterKeywords, StringComparison.InvariantCultureIgnoreCase));

            Definitions.AddRange(list);
        }

        public string SettingsPagePath => Resources.TabDocument;

        public string SettingsPageName => Resources.KeyMap;

        private KeyBindingDefinition[] definitions;

        public ObservableCollection<KeyBindingDefinition> Definitions { get; } = new();

        private bool isShowNotAssignOnly;
        public bool IsShowNotAssignOnly
        {
            get => isShowNotAssignOnly;
            set => Set(ref isShowNotAssignOnly, value);
        }

        private string filterKeywords;
        public string FilterKeywords
        {
            get => filterKeywords;
            set => Set(ref filterKeywords, value);
        }

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
                if (dialog.ConflictDefinition is KeyBindingDefinition conflictDefinition)
                    keybindingManager.ChangeKeyBinding(conflictDefinition, Key.None, ModifierKeys.None);

                if (KeyBindingDefinition.TryParseExpression(dialog.CurrentExpression, out var newKey, out var newModifier))
                    keybindingManager.ChangeKeyBinding(definition, newKey, newModifier);
            }
            UpdateDisplayList();
        }
        public void ResetAllDefinitions()
        {
            if (MessageBox.Show(Resources.ComfirmResetAllKeybindingDefinitions, Resources.Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            foreach (var definition in Definitions)
                keybindingManager.DefaultKeyBinding(definition);
            UpdateDisplayList();
        }
    }
}