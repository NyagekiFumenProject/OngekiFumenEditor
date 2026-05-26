using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace OngekiFumenEditor
{
    public partial class App : Application
    {
        public bool IsGUIMode { get; }

        public App(bool isGUIMode = true)
        {
            CheckOrUpgradeAllSettings();

            IsGUIMode = isGUIMode;
        }

        public static void CheckOrUpgradeAllSettings()
        {
            FileLogOutput.WriteLog($"current setting file:{ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath}\n");

            if (!OngekiFumenEditor.Properties.ProgramSetting.Default.__NeedUpgradeSetting)
                return;

            FileLogOutput.WriteLog($"Begin upgrade program settings\n");

            IEnumerable<Assembly> assemblyList = [
                typeof(Properties.AudioSetting).Assembly,
                typeof(Gemini.AppBootstrapper).Assembly
            ];

            var settingsTypes = assemblyList.Distinct()
                .SelectMany(x => x.GetTypes().Where(t => typeof(ApplicationSettingsBase).IsAssignableFrom(t)))
                .ToList();

            foreach (var type in settingsTypes)
            {
                try
                {
                    var defaultProperty = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                    var settings = defaultProperty?.GetValue(null) as ApplicationSettingsBase;
                    settings?.Upgrade();
                    settings?.Reload();
                    settings?.Save();
                    FileLogOutput.WriteLog($"upgrade setting successfully: {type.FullName}\n");
                }
                catch (Exception ex)
                {
                    FileLogOutput.WriteLog($"upgrade setting failed: {type.FullName}: {ex}\n");
                }
            }

            OngekiFumenEditor.Properties.ProgramSetting.Default.__NeedUpgradeSetting = false;
            OngekiFumenEditor.Properties.ProgramSetting.Default.Save();

            FileLogOutput.WriteLog($"Upgrade program settings finished\n");
        }
    }
}
