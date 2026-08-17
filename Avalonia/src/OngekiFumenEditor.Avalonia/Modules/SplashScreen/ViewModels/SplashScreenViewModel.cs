using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Views;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.OpenUrlCommon;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

[RegisterSingleton<ISplashScreenWindow>]
public partial class SplashScreenViewModel : WindowViewModelBase, ISplashScreenWindow
{
    private readonly ILanguageManager languageManager;
    private readonly ISettingManager settingManager;
    private readonly IEditorRecentFilesManager recentFilesManager;
    private readonly IFumenVisualEditorProvider editorProvider;
    private readonly ICommandService commandService;
    private readonly IRecentRecordValidityCoordinator recentRecordValidityCoordinator;
    private readonly IShell shell;
    private readonly string initialLanguage;
    private string selectedLanguage;

    public SplashScreenViewModel(
        ILanguageManager languageManager,
        ISettingManager settingManager,
        IEditorRecentFilesManager recentFilesManager,
        IRecentRecordValidityCoordinator recentRecordValidityCoordinator,
        IFumenVisualEditorProvider editorProvider,
        ICommandService commandService,
        IShell shell)
    {
        this.languageManager = languageManager;
        this.settingManager = settingManager;
        this.recentFilesManager = recentFilesManager;
        this.recentRecordValidityCoordinator = recentRecordValidityCoordinator;
        this.editorProvider = editorProvider;
        this.commandService = commandService;
        this.shell = shell;

        Languages = languageManager.GetAvaliableLanguageNames().ToArray();
        initialLanguage = languageManager.GetCurrentLanguage() ?? "Default";
        selectedLanguage = initialLanguage;
    }

    public WindowViewModelBase WindowViewModel => this;

    public bool CanCreateNew => editorProvider.CanCreateNew;

    public IReadOnlyList<string> Languages { get; }

    public string SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !SetProperty(ref selectedLanguage, value))
                return;

            languageManager.SetLanguage(value);

            var setting = settingManager.GetSetting(GekiminiSetting.JsonTypeInfo);
            setting.LanguageCode = languageManager.GetCurrentLanguage();
            settingManager.SaveSetting(setting, GekiminiSetting.JsonTypeInfo);

            OnPropertyChanged(nameof(IsRequestRestartProgram));
            RefreshRecentFiles();
        }
    }

    public bool IsRequestRestartProgram =>
        !string.Equals(initialLanguage, SelectedLanguage, StringComparison.OrdinalIgnoreCase);

    public bool DisableShowSplashScreenAfterBoot
    {
        get => ProgramSetting.Default.DisableShowSplashScreenAfterBoot;
        set
        {
            if (ProgramSetting.Default.DisableShowSplashScreenAfterBoot == value)
                return;

            ProgramSetting.Default.DisableShowSplashScreenAfterBoot = value;
            ProgramSetting.Default.Save();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<GroupedItem> GroupedItems { get; } = [];

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        recentRecordValidityCoordinator.BeginValidationGeneration();
        GroupedItems.Clear();

        foreach (var group in recentFilesManager.RecentRecordInfos
                     .OrderByDescending(x => x.LastAccessTime)
                     .GroupBy(x => GroupByDateTime(x.LastAccessTime)))
        {
            GroupedItems.Add(new GroupedItem(
                group.Key,
                group.Select(x => new RecentFileItemViewModel(x, commandService)).ToArray()));
        }
    }

    private static string GroupByDateTime(DateTime? value)
    {
        if (value is not { } date)
            return Lang.Earlier;

        var elapsed = DateTime.Now - date;
        if (elapsed < TimeSpan.FromMinutes(15))
            return Lang.JustBefore;
        if (elapsed < TimeSpan.FromMinutes(30))
            return Lang.HalfHourAgo;
        if (elapsed < TimeSpan.FromHours(24))
            return Lang.Today;
        if (elapsed < TimeSpan.FromDays(7))
            return Lang.WithinOneWeek;
        if (elapsed < TimeSpan.FromDays(30))
            return Lang.WithinOneMonth;
        return Lang.Earlier;
    }

    [RelayCommand]
    private async Task CreateNewProjectAsync()
    {
        if (!editorProvider.CanCreateNew)
            return;

        var editor = editorProvider.Create();
        if (await editorProvider.TryNew(editor))
            await shell.OpenDocumentAsync(editor);
        else if (editor is IDisposable disposable)
            disposable.Dispose();
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var editor = editorProvider.Create();
        if (await editorProvider.TryOpen(editor))
            await shell.OpenDocumentAsync(editor);
        else if (editor is IDisposable disposable)
            disposable.Dispose();
    }

    [RelayCommand]
    private static Task FastOpenAsync()
    {
        return CommandRouterHelper.ExecuteCommand(new Command(new FastOpenFumenCommandDefinition()));
    }

    [RelayCommand]
    private static Task OpenTutorialAsync()
    {
        return CommandRouterHelper.ExecuteCommand(new Command(new UsageWikiCommandDefinition()));
    }
}
