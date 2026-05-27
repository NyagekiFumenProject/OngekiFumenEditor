using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils.Settings
{
    /// <summary>
    /// 通过反射改写 System.Configuration.ClientConfigPaths.s_current，
    /// 让 LocalFileSettingsProvider 把 user.config 读写到 &lt;exe&gt;\Config\user.config，
    /// 替代默认的 %LOCALAPPDATA%\&lt;App&gt;_&lt;hash&gt;\&lt;version&gt;\user.config。
    /// </summary>
    internal static class PortableUserConfigPathsPatcher
    {
        private static bool _applied;

        public static string ConfigDirectory { get; private set; }
        public static string ConfigFilePath { get; private set; }
        public static bool IsApplied => _applied;

        public static void Apply()
        {
            if (_applied)
                return;

            try
            {
                var baseDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrEmpty(baseDir))
                    baseDir = AppContext.BaseDirectory;

                ConfigDirectory = Path.Combine(baseDir, "Config");
                ConfigFilePath = Path.Combine(ConfigDirectory, "user.config");
                Directory.CreateDirectory(ConfigDirectory);

                var asm = typeof(System.Configuration.ConfigurationManager).Assembly;
                var clientConfigPathsType = asm.GetType("System.Configuration.ClientConfigPaths");
                if (clientConfigPathsType is null)
                {
                    FileLogOutput.WriteLog("[PortableUserConfigPathsPatcher] ClientConfigPaths type not found, skip patch.\n");
                    return;
                }

                var instance = RuntimeHelpers.GetUninitializedObject(clientConfigPathsType);

                var processPath = Environment.ProcessPath ?? string.Empty;
                var appConfigUri = string.IsNullOrEmpty(processPath)
                    ? null
                    : new Uri(processPath + ".config").AbsoluteUri;

                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

                SetField(clientConfigPathsType, instance, "_includesUserConfig", true);
                SetField(clientConfigPathsType, instance, new[] { "_hasEntryAssembly", "<HasEntryAssembly>k__BackingField" }, true);
                SetField(clientConfigPathsType, instance, new[] { "_localConfigDirectory", "<LocalConfigDirectory>k__BackingField" }, ConfigDirectory);
                SetField(clientConfigPathsType, instance, new[] { "_roamingConfigDirectory", "<RoamingConfigDirectory>k__BackingField" }, ConfigDirectory);
                SetField(clientConfigPathsType, instance, new[] { "_localConfigFilename", "<LocalConfigFilename>k__BackingField" }, ConfigFilePath);
                SetField(clientConfigPathsType, instance, new[] { "_roamingConfigFilename", "<RoamingConfigFilename>k__BackingField" }, ConfigFilePath);
                if (appConfigUri is not null)
                {
                    SetField(clientConfigPathsType, instance, new[] { "_applicationConfigUri", "<ApplicationConfigUri>k__BackingField" }, appConfigUri);
                    SetField(clientConfigPathsType, instance, new[] { "<ApplicationUri>k__BackingField" }, processPath);
                }
                SetField(clientConfigPathsType, instance, "_companyName", "OngekiFumenEditor");
                SetField(clientConfigPathsType, instance, new[] { "_productName", "<ProductName>k__BackingField" }, "OngekiFumenEditor");
                SetField(clientConfigPathsType, instance, new[] { "_productVersion", "<ProductVersion>k__BackingField" }, version);

                SetStaticField(clientConfigPathsType, "s_current", instance);
                SetStaticField(clientConfigPathsType, "s_currentIncludesUserConfig", true);

                _applied = true;
                FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] Patched user.config path -> {ConfigFilePath}\n");

                try
                {
                    var currentProp = clientConfigPathsType.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    var current = currentProp?.GetValue(null);
                    var lcfProp = clientConfigPathsType.GetProperty("LocalConfigFilename", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var lcfValue = lcfProp?.GetValue(current) as string;
                    var assemblyLoc = clientConfigPathsType.Assembly.Location;
                    var assemblyName = clientConfigPathsType.Assembly.FullName;
                    FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] verify: Current==instance={ReferenceEquals(current, instance)}, LocalConfigFilename={lcfValue}\n");
                    FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] ConfigurationManager assembly: {assemblyName} @ {assemblyLoc}\n");
                }
                catch (Exception ex)
                {
                    FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] verify failed: {ex}\n");
                }
            }
            catch (Exception ex)
            {
                FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] Apply failed, fallback to default LocalAppData: {ex}\n");
            }
        }

        private static void SetField(Type type, object instance, string name, object value)
        {
            SetField(type, instance, new[] { name }, value);
        }

        private static void SetField(Type type, object instance, string[] candidateNames, object value)
        {
            foreach (var name in candidateNames)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field is not null)
                {
                    field.SetValue(instance, value);
                    return;
                }
            }
            FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] instance field not found: {string.Join("/", candidateNames)}\n");
        }

        private static void SetStaticField(Type type, string name, object value)
        {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (field is null)
            {
                FileLogOutput.WriteLog($"[PortableUserConfigPathsPatcher] static field not found: {name}\n");
                return;
            }
            field.SetValue(null, value);
        }
    }
}
