using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.SettingPages.Mcp.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class McpSettingViewModel : PropertyChangedBase, ISettingsEditor
    {
        public ProgramSetting Setting => ProgramSetting.Default;

        public string SettingsPageName => Resources.TabMcp;

        public string SettingsPagePath => Resources.TabEnviorment;

        public void ApplyChanges()
        {
            Setting.Save();
        }
    }
}
