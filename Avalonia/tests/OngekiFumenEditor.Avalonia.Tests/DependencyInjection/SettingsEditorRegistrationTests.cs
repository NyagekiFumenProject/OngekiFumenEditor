using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.Modules.Settings.ViewModels;
using Gekimini.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.Views;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Views;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Views;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.Views;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.Views;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.DependencyInjection;

public sealed class SettingsEditorRegistrationTests
{
    private static readonly Type[] ExpectedEditorTypes =
    [
        typeof(AudioSettingViewModel),
        typeof(FumenVisualEditorColorSettingViewModel),
        typeof(FumenVisualEditorGlobalSettingViewModel),
        typeof(KeyBindingSettingViewModel),
        typeof(LogsSettingViewModel),
        typeof(ProgramSettingViewModel)
    ];

    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersAllSettingsEditorsAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();

        var descriptors = services
            .Where(service => service.ServiceType == typeof(ISettingsEditor))
            .ToArray();

        Assert.Equal(ExpectedEditorTypes.Length, descriptors.Length);
        Assert.All(descriptors, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
        Assert.Equal(
            ExpectedEditorTypes.OrderBy(type => type.FullName, StringComparer.Ordinal),
            descriptors.Select(descriptor => descriptor.ImplementationType!)
                .OrderBy(type => type.FullName, StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public void RegisteredSettingsEditors_BuildExpectedPageTreeAndMatchingViews()
    {
        var application = Assert.IsAssignableFrom<global::Gekimini.Avalonia.App>(Application.Current);
        var editors = application.ServiceProvider
            .GetServices<ISettingsEditor>()
            .Where(editor => ExpectedEditorTypes.Contains(editor.GetType()))
            .DistinctBy(editor => editor.GetType())
            .ToArray();
        var expectedEditors = new (Type EditorType, string Name, string Path, Type ViewType)[]
        {
            (typeof(ProgramSettingViewModel), Lang.TabProgram, Lang.TabEnviorment, typeof(ProgramSettingView)),
            (typeof(LogsSettingViewModel), Lang.TabLogger, Lang.TabEnviorment, typeof(LogsSettingView)),
            (typeof(KeyBindingSettingViewModel), Lang.KeyMap, Lang.TabDocument, typeof(KeyBindingSettingView)),
            (typeof(FumenVisualEditorGlobalSettingViewModel), Lang.TabEditor, Lang.TabDocument, typeof(FumenVisualEditorGlobalSettingView)),
            (typeof(FumenVisualEditorColorSettingViewModel), Lang.VisualEditorLaneColorSettings, Lang.TabDocument + "\\" + Lang.TabEditor, typeof(FumenVisualEditorColorSettingView)),
            (typeof(AudioSettingViewModel), Lang.TabAudio, Lang.TabSound, typeof(AudioSettingView))
        };

        Assert.Equal(ExpectedEditorTypes.Length, editors.Length);
        Assert.All(expectedEditors, expected =>
        {
            var editor = Assert.Single(editors, item => item.GetType() == expected.EditorType);
            Assert.Equal(expected.Name, editor.SettingsPageName);
            Assert.Equal(expected.Path, editor.SettingsPagePath);
        });

        var settings = new SettingsViewModel(editors);
        Assert.Equal(3, settings.Pages.Count);

        var environmentPage = GetRequiredPage(settings.Pages, Lang.TabEnviorment);
        AssertEditor<ProgramSettingViewModel>(GetRequiredPage(environmentPage.Children, Lang.TabProgram));
        AssertEditor<LogsSettingViewModel>(GetRequiredPage(environmentPage.Children, Lang.TabLogger));

        var documentPage = GetRequiredPage(settings.Pages, Lang.TabDocument);
        AssertEditor<KeyBindingSettingViewModel>(GetRequiredPage(documentPage.Children, Lang.KeyMap));
        var editorPage = GetRequiredPage(documentPage.Children, Lang.TabEditor);
        AssertEditor<FumenVisualEditorGlobalSettingViewModel>(editorPage);
        AssertEditor<FumenVisualEditorColorSettingViewModel>(
            GetRequiredPage(editorPage.Children, Lang.VisualEditorLaneColorSettings));

        var soundPage = GetRequiredPage(settings.Pages, Lang.TabSound);
        AssertEditor<AudioSettingViewModel>(GetRequiredPage(soundPage.Children, Lang.TabAudio));

        var viewLocator = application.ServiceProvider.GetRequiredService<ViewLocator>();
        Assert.All(expectedEditors, expected =>
        {
            var editor = Assert.Single(editors, item => item.GetType() == expected.EditorType);
            var view = viewLocator.Build(editor);

            Assert.Equal(expected.ViewType, view.GetType());
            Assert.Same(editor, view.DataContext);
        });
    }

    [AvaloniaFact]
    public void FumenVisualEditorGlobalSettingView_UndoLimitControlsUseTwoWayBindings()
    {
        var setting = EditorGlobalSetting.Default;
        var originalEnabled = setting.IsEnableUndoActionSavingLimit;
        var originalLimit = setting.UndoActionSavingLimit;

        try
        {
            setting.IsEnableUndoActionSavingLimit = false;
            setting.UndoActionSavingLimit = 50;
            var view = new FumenVisualEditorGlobalSettingView
            {
                DataContext = new FumenVisualEditorGlobalSettingViewModel()
            };
            var enabledCheckBox = Assert.IsType<CheckBox>(
                view.FindControl<CheckBox>("UndoHistoryLimitEnabledCheckBox"));
            var editorPanel = Assert.IsType<StackPanel>(
                view.FindControl<StackPanel>("UndoHistoryLimitEditor"));
            var limitTextBox = Assert.IsType<TextBox>(
                view.FindControl<TextBox>("UndoHistoryLimitTextBox"));

            Assert.False(enabledCheckBox.IsChecked);
            Assert.False(editorPanel.IsEnabled);
            Assert.Equal("50", limitTextBox.Text);

            enabledCheckBox.IsChecked = true;
            Assert.True(setting.IsEnableUndoActionSavingLimit);
            Assert.True(editorPanel.IsEnabled);

            limitTextBox.Text = "17";
            Assert.Equal(17, setting.UndoActionSavingLimit);

            setting.IsEnableUndoActionSavingLimit = false;
            Assert.False(enabledCheckBox.IsChecked);
            Assert.False(editorPanel.IsEnabled);
        }
        finally
        {
            setting.IsEnableUndoActionSavingLimit = originalEnabled;
            setting.UndoActionSavingLimit = originalLimit;
        }
    }

    [AvaloniaFact]
    public void LogsSettingView_ShowsEffectivePlatformFolderAsReadOnly()
    {
        const string effectivePath = "opfs:/logs";
        var view = new LogsSettingView
        {
            DataContext = new LogsSettingViewModel(new ILogOutput[] { new StubFileLogOutput(effectivePath) })
        };

        var pathTextBox = Assert.IsType<TextBox>(
            view.FindControl<TextBox>("LogFolderPathTextBox"));

        Assert.True(pathTextBox.IsReadOnly);
        Assert.Equal(effectivePath, pathTextBox.Text);
    }

    private static SettingsPageViewModel GetRequiredPage(
        IEnumerable<SettingsPageViewModel> pages,
        string name)
    {
        return Assert.Single(pages, page => page.Name == name);
    }

    private static void AssertEditor<TEditor>(SettingsPageViewModel page)
        where TEditor : ISettingsEditor
    {
        Assert.IsType<TEditor>(Assert.Single(page.Editors));
    }

    private sealed class StubFileLogOutput(string path) : IFileLogOutput
    {
        public string LogDirectoryPath { get; } = path;

        public void WriteLog(ILogOutput.Severity severity, string content)
        {
        }
    }
}
