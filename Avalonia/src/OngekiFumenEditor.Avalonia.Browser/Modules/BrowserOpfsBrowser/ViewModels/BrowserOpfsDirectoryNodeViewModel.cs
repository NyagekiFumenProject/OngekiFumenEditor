#nullable enable

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;

public sealed class BrowserOpfsDirectoryNodeViewModel : ObservableObject
{
    private readonly BrowserOpfsBrowserViewModel? owner;
    private bool isExpanded;

    private BrowserOpfsDirectoryNodeViewModel(
        BrowserOpfsBrowserViewModel? owner,
        string name,
        string relativePath,
        bool isPlaceholder,
        bool isExpanded)
    {
        this.owner = owner;
        Name = name;
        RelativePath = relativePath;
        IsPlaceholder = isPlaceholder;
        this.isExpanded = isExpanded;
    }

    public string Name { get; }
    public string RelativePath { get; }
    public bool IsPlaceholder { get; }
    public bool IsLoaded { get; internal set; }
    public ObservableCollection<BrowserOpfsDirectoryNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (!SetProperty(ref isExpanded, value) || !value || IsPlaceholder)
                return;
            owner?.RequestExpand(this);
        }
    }

    internal static BrowserOpfsDirectoryNodeViewModel CreateRoot(BrowserOpfsBrowserViewModel owner) =>
        new(owner, "OPFS", string.Empty, false, true);

    internal static BrowserOpfsDirectoryNodeViewModel CreateDirectory(
        BrowserOpfsBrowserViewModel owner,
        string name,
        string relativePath)
    {
        var node = new BrowserOpfsDirectoryNodeViewModel(owner, name, relativePath, false, false);
        node.Children.Add(CreatePlaceholder());
        return node;
    }

    internal static BrowserOpfsDirectoryNodeViewModel CreatePlaceholder() =>
        new(null, string.Empty, string.Empty, true, false);
}

public sealed record BrowserOpfsBreadcrumbViewModel(string Name, string RelativePath, bool IsRoot);
