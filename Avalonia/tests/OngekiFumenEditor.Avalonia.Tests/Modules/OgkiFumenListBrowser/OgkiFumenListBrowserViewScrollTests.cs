using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Iciclecreek.Avalonia.WindowManager;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Views;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.OgkiFumenListBrowser;

public sealed class OgkiFumenListBrowserViewScrollTests
{
    [AvaloniaFact]
    public async Task ExpandingResultKeepsItsViewportPosition()
    {
        var view = new OgkiFumenListBrowserView();
        var windowsPanel = new WindowsPanel();
        var window = new Window
        {
            Width = 760,
            Height = 500,
            Content = windowsPanel
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            view.Show(windowsPanel);
            window.UpdateLayout();

            var scrollViewer = Assert.Single(
                view.GetVisualDescendants().OfType<ScrollViewer>(),
                static candidate => candidate.GetVisualDescendants().OfType<ItemsControl>().Any());
            scrollViewer.IsVisible = true;
            var itemsControl = Assert.Single(scrollViewer.GetVisualDescendants().OfType<ItemsControl>());
            itemsControl.ItemsSource = CreateSets(16);
            window.UpdateLayout();

            Assert.IsType<StackPanel>(itemsControl.Presenter?.Panel);
            var expanders = view.GetVisualDescendants().OfType<Expander>().ToArray();
            Assert.Equal(16, expanders.Length);

            scrollViewer.Offset = new Vector(0, 360);
            window.UpdateLayout();

            var target = expanders[7];
            var headerToggleButton = Assert.Single(target.GetVisualDescendants().OfType<ToggleButton>());
            Assert.False(headerToggleButton.Focusable);
            Assert.False(headerToggleButton.IsTabStop);
            var bringIntoView = new RequestBringIntoViewEventArgs
            {
                RoutedEvent = Control.RequestBringIntoViewEvent,
                TargetObject = headerToggleButton
            };
            headerToggleButton.RaiseEvent(bringIntoView);
            Assert.True(bringIntoView.Handled);
            var beforeTop = target.TranslatePoint(new Point(0, 0), scrollViewer)?.Y;
            Assert.NotNull(beforeTop);

            target.IsExpanded = true;
            window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(static () => { });
            window.UpdateLayout();

            var afterTop = target.TranslatePoint(new Point(0, 0), scrollViewer)?.Y;
            Assert.NotNull(afterTop);
            Assert.InRange(afterTop!.Value - beforeTop!.Value, -1, 1);
        }
        finally
        {
            if (view.IsVisible)
                view.Close();
            window.Close();
        }
    }

    private static OngekiFumenSet[] CreateSets(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var file = new StubFile($"{index}.xml");
                var set = new OngekiFumenSet(file, $"music/{index}", index, index, $"Track {index}", "Artist", "Genre");
                set.Difficults.Add(new OngekiFumenDiff(set)
                {
                    DiffIdx = 0,
                    Level = 1,
                    Bpm = 120,
                    Creator = "Creator",
                    FumenFile = file,
                    FumenLocator = $"fumen/{index}.ogkr"
                });
                return set;
            })
            .ToArray();
    }

    private sealed class StubFile(string fileName) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => fileName;
        public string? LocalPath => null;
        public string FileName => fileName;
        public long FileLength => 0;
        public ValueTask<string[]> ReadAllLines() => ValueTask.FromResult(Array.Empty<string>());
        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Array.Empty<byte>());
        public Task<Stream> OpenRead() => Task.FromResult<Stream>(new MemoryStream());
        public Task<Stream> OpenWrite() => Task.FromResult<Stream>(new MemoryStream());
        public void Dispose()
        {
        }
    }
}
