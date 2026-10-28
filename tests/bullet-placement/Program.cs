using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Windows;
using Caliburn.Micro;
using Gemini.Framework.Themes;
using OngekiFumenEditor;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Kernel.SettingPages.KeyBinding.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.ValueConverters;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var checks = 0;
        try
        {
            var repositoryRoot = FindRepository(args.FirstOrDefault());
            Directory.SetCurrentDirectory(Path.Combine(repositoryRoot, "OngekiFumenEditor/bin/Debug/net10.0-windows"));
            _ = new App(false);
            _ = new AppBootstrapper(false) { IsGUIMode = false };
            PlatformProvider.Current = new DefaultPlatformProvider();
            using var settings = new SettingsScope();
            var previousWindow = Application.Current.MainWindow;
            var hiddenWindow = new Window { ShowInTaskbar = false, ShowActivated = false };
            Application.Current.MainWindow = hiddenWindow;
            try
            {
                Application.Current.Resources["NullToZeroConverter"] = new NullToZeroConverter();
                Application.Current.Resources["SelectionMovableItemsCheckConverter"] = new SelectionMovableItemsCheckConverter();
                Application.Current.Resources["SelectionFullLaneCheckConverter"] = new SelectionFullLaneCheckConverter();
                Application.Current.Resources["SelectionLaneObjectsCheckConverter"] = new SelectionLaneObjectsCheckConverter();
                Application.Current.Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();
                var theme = IoC.Get<IThemeManager>().Themes.First(x => x.GetType().Name.Contains("Dark"));
                foreach (var uri in theme.ApplicationResources.Concat(theme.MainWindowResources))
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml") });
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Blue.xaml") });
                void Check(bool condition, string label)
                {
                    if (!condition)
                        throw new InvalidOperationException("FAIL: " + label);
                    checks++;
                    Console.WriteLine("PASS: " + label);
                }
                BehaviorChecks.Run(Check, repositoryRoot);
                DeactivationChecks.Run(Check);
                LifecycleEventChecks.Run(Check);
                RenderingChecks.Run(Check, repositoryRoot);
                Check(!hiddenWindow.IsVisible, "bootstrap window was never shown");
                Console.WriteLine($"RESULT: {checks} assertions passed.");
                Console.WriteLine("BOUNDARY: synthetic WPF routing and public gesture APIs are not physical mouse/keyboard input. Native focus transfer, floating-window activation, OLE drag startup/capture and persisted key-config reload require separate interactive verification. No window was shown.");
                return 0;
            }
            finally
            {
                Application.Current.MainWindow = previousWindow;
                hiddenWindow.Close();
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static string FindRepository(string? argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            var path = Path.GetFullPath(argument);
            if (File.Exists(Path.Combine(path, "OngekiFumenEditor/OngekiFumenEditor.csproj")))
                return path;
            throw new DirectoryNotFoundException("Not a repository root: " + path);
        }
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor/OngekiFumenEditor.csproj")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Pass the repository root as the first argument.");
    }

    private sealed class SettingsScope : IDisposable
    {
        private readonly Dictionary<string, object> values = EditorGlobalSetting.Default.Properties.Cast<SettingsProperty>()
            .Where(x => !x.IsReadOnly).ToDictionary(x => x.Name, x => EditorGlobalSetting.Default[x.Name]);
        private readonly (KeyBindingDefinition Definition, System.Windows.Input.Key Key, System.Windows.Input.ModifierKeys Modifiers)[] keys =
            new KeyBindingSettingViewModel().Definitions.Select(x => (x, x.Key, x.Modifiers)).ToArray();
        private readonly int randomSeed = BulletPallete.RandomSeed;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? resourceCulture = Resources.Culture;

        public SettingsScope()
        {
            // EditorSetting normally saves after a delay. Never persist test values, even during dispatcher pumps.
            EditorGlobalSetting.Default.SettingsSaving += PreventSave;
            EditorGlobalSetting.Default.IsEnableAutoSave = false;
            EditorGlobalSetting.Default.RecoveryCurrentTimeAfterExecuteAction = false;
            EditorGlobalSetting.Default.IsEnableUndoActionSavingLimit = false;
            EditorGlobalSetting.Default.EnablePlaceObjectBeyondAudioDuration = false;
        }

        private static void PreventSave(object? sender, CancelEventArgs e) => e.Cancel = true;

        public void Dispose()
        {
            foreach (var entry in values)
                EditorGlobalSetting.Default[entry.Key] = entry.Value;
            foreach (var (definition, key, modifiers) in keys)
            {
                definition.Key = key;
                definition.Modifiers = modifiers;
            }
            BulletPallete.RandomSeed = randomSeed;
            CultureInfo.CurrentUICulture = uiCulture;
            Resources.Culture = resourceCulture;
            EditorGlobalSetting.Default.SettingsSaving -= PreventSave;
        }
    }
}
