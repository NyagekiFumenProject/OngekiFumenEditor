using OngekiFumenEditor.Base.Collections.Base.NotQuadTree;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace OngekiFumenEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public bool IsGUIMode { get; }

        public App(bool isGUIMode = true)
        {
            CheckOrUpgradeAllSettings();

            AppDomain.CurrentDomain.AssemblyResolve += OnSatelliteAssemblyResolve;
            // 设置工作目录为执行文件所在的目录
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            IsGUIMode = isGUIMode;
        }

        public static void CheckOrUpgradeAllSettings()
        {
            FileLogOutput.WriteLog($"current setting file:{ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath}\n");

            if (!OngekiFumenEditor.Properties.ProgramSetting.Default.__NeedUpgradeSetting)
                return;
            /*
             fuck M$ why not auto-upgrade for other settings
             */

            FileLogOutput.WriteLog($"Begin upgrade program settings\n");

            //only upgrade settings in these assembly:
            IEnumerable<Assembly> assemblyList = [
                typeof(Properties.AudioSetting).Assembly,
                typeof(Gemini.AppBootstrapper).Assembly
                ];

            var settingsTypes = assemblyList.Distinct().SelectMany(
                x => x.GetTypes()
                .Where(t => typeof(ApplicationSettingsBase).IsAssignableFrom(t))
                )
                .ToList();

            foreach (var type in settingsTypes)
            {
                try
                {
                    var defaultProperty = type.GetProperty("Default",
                        BindingFlags.Public | BindingFlags.Static);

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

        private Assembly OnSatelliteAssemblyResolve(object sender, ResolveEventArgs args)
        {
            /*
             这里解决Costura.Fody无法正常加载SatelliteAssembly的问题
             */

            byte[] ReadStream(Stream stream)
            {
                if (stream is null)
                    return default;

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }

            byte[] LoadStream(string dllResName)
            {
                var executingAssembly = Assembly.GetExecutingAssembly();

                byte[] LoadStreamInternal(string fullResName)
                {
                    using var manifestResourceStream = executingAssembly.GetManifestResourceStream(fullResName);
                    return ReadStream(manifestResourceStream);
                }

                var fixedName = $"costura.{dllResName}.dll";

                if (LoadStreamInternal(fixedName) is byte[] uncompressData)
                    return uncompressData;

                var fixedCompressedName = $"{fixedName}.compressed";

                if (LoadStreamInternal(fixedCompressedName) is byte[] compressedData)
                {
                    var stream = new MemoryStream(compressedData);
                    using (var deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
                    {
                        var memoryStream = new MemoryStream();
                        deflateStream.CopyTo(memoryStream);
                        memoryStream.Position = 0L;
                        return memoryStream.ToArray();
                    }
                }

                return default;
            }

            Assembly ReadFromEmbeddedResources(AssemblyName requestedAssemblyName)
            {
                var name = requestedAssemblyName.Name.ToLowerInvariant();
                if (requestedAssemblyName.CultureInfo != null && !string.IsNullOrEmpty(requestedAssemblyName.CultureInfo.Name))
                {
                    /*
                     * 这里Costura.Fody没有对cultureName进行一个小写转换导致的错误
                     * Costura.Fody打包进去的资源名字均小写,因此无法可能无法加载有大写的资源名字
                     */
                    name = requestedAssemblyName.CultureInfo.Name + "." + name;
                    name = name.ToLowerInvariant(); //fix
                }

                var assemblyData = LoadStream(name);
                if (assemblyData is null)
                    return null;
                return Assembly.Load(assemblyData);
            }

            AssemblyName requestedAssemblyName = new AssemblyName(args.Name);
            if (!(requestedAssemblyName.Name.EndsWith(".resources") && !string.IsNullOrWhiteSpace(requestedAssemblyName.CultureName)))
                return default;
            Debug.WriteLine($"try resolve satellite assemblies:{requestedAssemblyName.Name} ({requestedAssemblyName.CultureName})");
            var assembly = ReadFromEmbeddedResources(requestedAssemblyName);
            if (assembly is not null)
                Debug.WriteLine($"\t  resolve satellite assemblies GOOD :{assembly.FullName}");
            else
            {
                Debug.WriteLine($"\t  resolve satellite assemblies BAD");
            }
            return assembly;
        }
    }
}
