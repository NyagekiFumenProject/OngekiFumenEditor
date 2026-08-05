using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Modules.Window.Views;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Dialogs.Views;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class DialogViewModelTests
{
    [AvaloniaFact]
    public void ViewLocator_DialogViewModels_BuildsMatchingWindowViews()
    {
        var application = Assert.IsAssignableFrom<global::Gekimini.Avalonia.App>(Application.Current);
        var viewLocator = application.ServiceProvider.GetRequiredService<ViewLocator>();

        var pairs = new (WindowViewModelBase ViewModel, Type ViewType)[]
        {
            (new AboutWindowViewModel(), typeof(AboutWindowView)),
            (new CommonColorPickerViewModel(), typeof(CommonColorPickerView))
        };

        Assert.All(pairs, pair =>
        {
            var view = viewLocator.Build(pair.ViewModel);

            Assert.True(
                pair.ViewType == view.GetType(),
                view is TextBlock textBlock
                    ? textBlock.Text
                    : $"Expected {pair.ViewType.FullName}, actual {view.GetType().FullName}.");
            Assert.Same(pair.ViewModel, view.DataContext);
        });
    }

    [Fact]
    public void CommonColorPickerViewModel_CurrentColor_WritesThroughAndNotifies()
    {
        var selectedColor = Colors.Black;
        var setterCallCount = 0;
        var propertyNames = new List<string?>();
        var viewModel = new CommonColorPickerViewModel(
            () => selectedColor,
            color =>
            {
                selectedColor = color;
                setterCallCount++;
            },
            "Lane color");
        viewModel.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        viewModel.CurrentColor = Colors.Orange;

        Assert.Equal(Colors.Orange, selectedColor);
        Assert.Equal(Colors.Orange, viewModel.CurrentColor);
        Assert.Equal(1, setterCallCount);
        Assert.Equal([nameof(CommonColorPickerViewModel.CurrentColor)], propertyNames);
        Assert.Equal("Lane color", viewModel.Title);
    }

    [Fact]
    public void CommonColorPickerViewModel_SelectColorCommand_ParsesPaletteValue()
    {
        var selectedColor = Colors.Black;
        var viewModel = new CommonColorPickerViewModel(
            () => selectedColor,
            color => selectedColor = color,
            "Palette");

        viewModel.SelectColorCommand.Execute("#FF00FFFF");

        Assert.Equal(Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF), selectedColor);
        Assert.Equal(selectedColor, viewModel.CurrentColor);
    }

    [Fact]
    public void AboutWindowViewModel_UpdateNotification_ExposesSourceAndProductVersions()
    {
        var sourceVersion = new Version(1, 2, 3, 4);

        var viewModel = new AboutWindowViewModel(true, sourceVersion);

        Assert.True(viewModel.IsNotifyUpdateSuccess);
        Assert.Equal("1.2.3.4", viewModel.SourceVersion);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Version));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ProductVersion));
        Assert.Matches("^[0-9a-fA-F]{7}$", viewModel.CommitHash);
        Assert.Contains($"+{viewModel.CommitHash}", viewModel.ProductVersion, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^\\d{4}/\\d{1,2}/\\d{2} \\d{1,2}:\\d{2}:\\d{2}\\.\\d{3}$", viewModel.CommitDate);
        Assert.Matches("^\\d{4}/\\d{1,2}/\\d{2} \\d{1,2}:\\d{2}:\\d{2}\\.\\d{3}$", viewModel.BuildTime);

        var metadata = typeof(AboutWindowViewModel).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(x => x.Key, x => x.Value);
        Assert.True(DateTimeOffset.TryParse(
            metadata["GitCommitDate"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out _));
        Assert.True(DateTimeOffset.TryParse(
            metadata["BuildDateTime"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out _));
    }

    [Theory]
    [InlineData("1.2.3+0123456789abcdef", "1.2.3+0123456", "0123456")]
    [InlineData("1.2.3+0123456", "1.2.3+0123456", "0123456")]
    [InlineData("1.2.3-preview.1+linux.0123456789abcdef.dirty", "1.2.3-preview.1+linux.0123456.dirty", "0123456")]
    [InlineData("1.2.3+linux-x64", "1.2.3+linux-x64", "")]
    public void AboutWindowViewModel_ShortenProductVersion_OnlyTruncatesCommitMetadata(
        string informationalVersion,
        string expectedProductVersion,
        string expectedCommitHash)
    {
        var productVersion = AboutWindowViewModel.ShortenProductVersion(informationalVersion, out var commitHash);

        Assert.Equal(expectedProductVersion, productVersion);
        Assert.Equal(expectedCommitHash, commitHash);
    }

    [Fact]
    public async Task AboutCommandHandler_Run_ShowsAboutViewModelAsDialog()
    {
        var windowManager = new RecordingWindowManager();
        var handler = new AboutCommandHandler(windowManager);

        await handler.Run(null!);

        Assert.Equal(1, windowManager.ShowDialogCallCount);
        Assert.IsType<AboutWindowViewModel>(windowManager.DialogViewModel);
    }

    private sealed class RecordingWindowManager : IWindowManager
    {
        public int ShowDialogCallCount { get; private set; }
        public WindowViewModelBase? DialogViewModel { get; private set; }

        public Task ShowWindowAsync(WindowViewBase windowView) => Task.CompletedTask;

        public Task<bool?> ShowDialogAsync(WindowViewBase windowView) =>
            Task.FromResult<bool?>(null);

        public Task TryCloseWindowAsync(WindowViewBase windowView, bool dialogResult) =>
            Task.CompletedTask;

        public Task ShowWindowAsync(WindowViewModelBase windowViewModel) => Task.CompletedTask;

        public Task<bool?> ShowDialogAsync(WindowViewModelBase windowViewModel)
        {
            ShowDialogCallCount++;
            DialogViewModel = windowViewModel;
            return Task.FromResult<bool?>(null);
        }

        public Task TryCloseWindowAsync(WindowViewModelBase windowViewModelBase, bool dialogResult) =>
            Task.CompletedTask;
    }
}
