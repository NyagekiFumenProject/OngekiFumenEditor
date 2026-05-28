using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OngekiFumenEditor.Utils.Settings
{
    internal static class ApplicationSettingsBaseInjector
    {
        public static void EnsureInitializedAndInjectedProvider()
        {
            FileLogOutput.WriteLog($"current setting file:{ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath}\n");

            IEnumerable<Assembly> assemblyList = [
                typeof(Properties.AudioSetting).Assembly,
                typeof(Gemini.AppBootstrapper).Assembly
            ];

            var settings = assemblyList.Distinct()
                .SelectMany(x => x.GetTypes().Where(t => typeof(ApplicationSettingsBase).IsAssignableFrom(t)))
                .Select(type => type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null))
                .OfType<ApplicationSettingsBase>()
                .ToList();

            InjectJsonProvider(settings);

            //need't anymore
            //CheckOrUpgradeAllSettings(settings);
        }

        private static void InjectJsonProvider(List<ApplicationSettingsBase> settings)
        {
            var propInfo = typeof(ApplicationSettingsBase).GetProperty("Initializer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(
             nameof(ApplicationSettingsBase), "Initializer");


            foreach (var setting in settings)
            {
                var initializer = (SettingsProperty)propInfo.GetValue(setting);
                initializer.Provider = OverlayJsonSettingsProvider.Default;

                FileLogOutput.WriteLog($"setting {setting.GetType().Name} added OverlayJsonSettingsProvider\n");
            }
        }

        public static void CheckOrUpgradeAllSettings(List<ApplicationSettingsBase> settings)
        {
            if (!Properties.ProgramSetting.Default.__NeedUpgradeSetting)
                return;

            FileLogOutput.WriteLog($"Begin upgrade program settings\n");

            foreach (var setting in settings)
            {
                try
                {
                    setting?.Upgrade();
                    setting?.Reload();
                    setting?.Save();
                    FileLogOutput.WriteLog($"upgrade setting successfully: {setting.GetType().FullName}\n");
                }
                catch (Exception ex)
                {
                    FileLogOutput.WriteLog($"upgrade setting failed: {setting.GetType().FullName}: {ex}\n");
                }
            }

            Properties.ProgramSetting.Default.__NeedUpgradeSetting = false;
            Properties.ProgramSetting.Default.Save();

            FileLogOutput.WriteLog($"Upgrade program settings finished\n");
        }
    }
}
