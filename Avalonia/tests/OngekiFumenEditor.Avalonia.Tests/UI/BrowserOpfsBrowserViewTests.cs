#nullable enable

using System.Xml.Linq;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class BrowserOpfsBrowserViewTests
{
    [Fact]
    public void View_ContainsFileExplorerTreeAndFiveColumnDataGrid()
    {
        string repositoryRoot = FindRepositoryRoot();
        string viewPath = Path.Combine(
            repositoryRoot,
            "src",
            "OngekiFumenEditor.Avalonia.Browser",
            "Modules",
            "BrowserOpfsBrowser",
            "Views",
            "BrowserOpfsBrowserView.axaml");
        XDocument document = XDocument.Load(viewPath);
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement root = Assert.IsType<XElement>(document.Root);
        Assert.Equal(
            "OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Views.BrowserOpfsBrowserView",
            root.Attribute(xaml + "Class")?.Value);
        Assert.NotNull(root.Descendants(avalonia + "TreeView").SingleOrDefault());

        XElement entryGrid = Assert.Single(
            root.Descendants(avalonia + "DataGrid"),
            element => element.Attribute(xaml + "Name")?.Value == "EntryGrid");
        XElement columns = Assert.Single(entryGrid.Elements(avalonia + "DataGrid.Columns"));
        Assert.Equal(5, columns.Elements().Count());
        Assert.Contains(
            root.Descendants(),
            element => element.Attribute(xaml + "Name")?.Value == "FolderTreePanel");
        Assert.Contains(
            root.Descendants(),
            element => element.Attribute(xaml + "Name")?.Value == "FolderTreeSplitter");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }
}
