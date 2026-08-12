using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Themes;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Modules.MainMenu;
using Gekimini.Avalonia.Modules.MainMenu.Controls;
using Gekimini.Avalonia.Modules.MainMenu.ViewModels;
using Gekimini.Avalonia.Modules.MainMenu.Views;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gemini.Framework.Menus;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class MainMenuAutoHideTests
{
    [Fact]
    public void ApplyChanges_UpdatesExistingMenuAndRestoresOnNextConstruction()
    {
        var settingManager = new RecordingSettingManager(new GekiminiSetting());
        var menuViewModel = new MainMenuViewModel(new EmptyMenuBuilder(), settingManager);
        var settingsViewModel = new MainMenuSettingsViewModel(
            new StubThemeManager(),
            new StubLanguageManager(),
            settingManager)
        {
            AutoHideMainMenu = true
        };

        settingsViewModel.ApplyChanges();

        Assert.True(menuViewModel.AutoHide);
        Assert.True(settingManager.Setting.AutoHideMainMenu);
        Assert.Equal(1, settingManager.SaveCount);

        var restartedSettingManager = new RecordingSettingManager(new GekiminiSetting
        {
            AutoHideMainMenu = settingManager.SavedAutoHideMainMenu
        });
        var restartedMenu = new MainMenuViewModel(new EmptyMenuBuilder(), restartedSettingManager);

        Assert.True(restartedMenu.AutoHide);
    }

    [AvaloniaFact]
    public void MainMenuView_BindsAutoHideAndTracksLiveSettingChanges()
    {
        var settingManager = new RecordingSettingManager(new GekiminiSetting
        {
            AutoHideMainMenu = true
        });
        var viewModel = new MainMenuViewModel(new EmptyMenuBuilder(), settingManager);
        var view = new MainMenuView { DataContext = viewModel };
        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = view
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var menu = Assert.Single(view.GetVisualDescendants().OfType<MenuEx>());
            Assert.True(menu.AutoHide);
            Assert.True(menu.IsAutoHideCollapsed);

            settingManager.Setting.AutoHideMainMenu = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.AutoHide);
            Assert.False(menu.AutoHide);
            Assert.False(menu.IsAutoHideCollapsed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.F10)]
    public void KeyboardActivation_ExpandsAndFocusReturnCollapses(Key activationKey)
    {
        using var host = new MenuHost();
        host.ContentButton.Focus();

        Assert.True(host.Menu.IsAutoHideCollapsed);

        var physicalKey = activationKey == Key.F10 ? PhysicalKey.F10 : PhysicalKey.AltLeft;
        host.Window.KeyPress(activationKey, RawInputModifiers.None, physicalKey, string.Empty);
        Dispatcher.UIThread.RunJobs();

        Assert.False(host.Menu.IsAutoHideCollapsed);
        Assert.True(host.FirstItem.IsFocused);

        host.Window.KeyRelease(activationKey, RawInputModifiers.None, physicalKey, string.Empty);
        host.ContentButton.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.ContentButton.IsFocused);
        Assert.True(host.Menu.IsAutoHideCollapsed);
    }

    [AvaloniaFact]
    public void PointerEnteringCollapsedStrip_ExpandsAndLeavingCollapses()
    {
        using var host = new MenuHost();

        Assert.True(host.Menu.IsAutoHideCollapsed);
        Assert.Equal(2, host.Menu.Bounds.Height);

        host.Window.MouseMove(new Point(20, 1), RawInputModifiers.None);
        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.False(host.Menu.IsAutoHideCollapsed);
        Assert.True(host.Menu.Bounds.Height > 2);

        host.Window.MouseMove(new Point(200, 150), RawInputModifiers.None);
        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.Menu.IsAutoHideCollapsed);
        Assert.Equal(2, host.Menu.Bounds.Height);
    }

    [AvaloniaFact]
    public void OpenMenu_RemainsExpandedUntilClosedAndFocusReturnsToContent()
    {
        using var host = new MenuHost();

        host.Window.MouseMove(new Point(20, 1), RawInputModifiers.None);
        host.Window.UpdateLayout();
        host.FirstItem.IsSubMenuOpen = true;
        host.ContentButton.Focus();
        host.Window.MouseMove(new Point(200, 150), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.Menu.IsOpen);
        Assert.True(host.FirstItem.IsSubMenuOpen);
        Assert.False(host.Menu.IsAutoHideCollapsed);

        host.Menu.Close();
        host.ContentButton.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.False(host.Menu.IsOpen);
        Assert.True(host.ContentButton.IsFocused);
        Assert.True(host.Menu.IsAutoHideCollapsed);
    }

    private sealed class MenuHost : IDisposable
    {
        public MenuHost()
        {
            FirstItem = new MenuItem
            {
                Header = "_File",
                ItemsSource = new[] { new MenuItem { Header = "Open" } }
            };
            Menu = new MenuEx
            {
                AutoHide = true,
                ItemsSource = new[] { FirstItem }
            };
            ContentButton = new Button
            {
                Content = "Editor content",
                Background = Brushes.Transparent,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            root.Children.Add(Menu);
            root.Children.Add(ContentButton);
            Grid.SetRow(ContentButton, 1);

            Window = new Window
            {
                Width = 400,
                Height = 200,
                Content = root
            };
            Window.Show();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public Window Window { get; }
        public MenuEx Menu { get; }
        public MenuItem FirstItem { get; }
        public Button ContentButton { get; }

        public void Dispose()
        {
            Window.Close();
        }
    }

    private sealed class EmptyMenuBuilder : IMenuBuilder
    {
        public void BuildMenuBar(MenuBarDefinition menuBarDefinition, IMenu result)
        {
        }
    }

    private sealed class RecordingSettingManager : ISettingManager
    {
        public RecordingSettingManager(GekiminiSetting setting)
        {
            Setting = setting;
        }

        public GekiminiSetting Setting { get; }
        public int SaveCount { get; private set; }
        public bool SavedAutoHideMainMenu { get; private set; }

        public void SaveSetting<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
        {
            var setting = Assert.IsType<GekiminiSetting>(obj);
            SaveCount++;
            SavedAutoHideMainMenu = setting.AutoHideMainMenu;
        }

        public T GetSetting<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            JsonTypeInfo<T> jsonTypeInfo) where T : new()
        {
            Assert.Equal(typeof(GekiminiSetting), typeof(T));
            return (T)(object)Setting;
        }
    }

    private sealed class StubThemeManager : IThemeManager
    {
        public StubThemeManager()
        {
            CurrentColorTheme = new StubColorTheme();
            CurrentControlTheme = new StubControlTheme();
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<IColorTheme> AvaliableColorThemes => new[] { CurrentColorTheme };
        public IColorTheme CurrentColorTheme { get; set; }
        public IEnumerable<IControlTheme> AvaliableControlThemes => new[] { CurrentControlTheme };
        public IControlTheme CurrentControlTheme { get; set; }

        public void Initalize()
        {
        }
    }

    private sealed class StubColorTheme : IColorTheme
    {
        public string Name => "TestColor";

        public void ApplyColorTheme()
        {
        }

        public void RevertColorTheme()
        {
        }
    }

    private sealed class StubControlTheme : IControlTheme
    {
        public string Name => "TestControl";

        public void ApplyControlTheme()
        {
        }

        public void RevertControlTheme()
        {
        }
    }

    private sealed class StubLanguageManager : ILanguageManager
    {
        private string currentLanguage = "en";

        public IEnumerable<string> GetAvaliableLanguageNames() => new[] { currentLanguage };

        public void SetLanguage(string languageName)
        {
            currentLanguage = languageName;
        }

        public string GetCurrentLanguage() => currentLanguage;

        public string GetTranslatedText(string resKey) => resKey;

        public void Initialize()
        {
        }
    }
}
