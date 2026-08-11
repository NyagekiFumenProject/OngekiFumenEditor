#nullable enable

using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.BrowserOpfsBrowser;

public sealed class BrowserOpfsDownloadPlannerTests
{
    [Fact]
    public void Create_WithSingleFile_UsesDirectDownloadAndSanitizesSuggestedName()
    {
        var file = new BrowserOpfsEntrySnapshot(
            "report?.txt",
            "report?.txt",
            BrowserOpfsEntryKind.File,
            10,
            20);

        BrowserOpfsDownloadPlan plan = BrowserOpfsDownloadPlanner.Create([file], DateTimeOffset.UnixEpoch);

        Assert.False(plan.UseZip);
        Assert.Equal("report_.txt", plan.SuggestedFileName);
        Assert.Equal(file.RelativePath, Assert.Single(plan.SelectedEntries).RelativePath);
    }

    [Fact]
    public void Create_WithSingleFolder_UsesFolderNamedZip()
    {
        var folder = new BrowserOpfsEntrySnapshot(
            "charts",
            "charts",
            BrowserOpfsEntryKind.Folder,
            null,
            null);

        BrowserOpfsDownloadPlan plan = BrowserOpfsDownloadPlanner.Create([folder], DateTimeOffset.UnixEpoch);

        Assert.True(plan.UseZip);
        Assert.Equal("charts.zip", plan.SuggestedFileName);
    }

    [Fact]
    public void Create_WithParentFolderAndChildSelection_DeduplicatesChildAndUsesTimestampedZip()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 34, 56, TimeSpan.Zero);
        var folder = new BrowserOpfsEntrySnapshot(
            "charts",
            "charts",
            BrowserOpfsEntryKind.Folder,
            null,
            null);
        var child = new BrowserOpfsEntrySnapshot(
            "a.ogkr",
            "charts/a.ogkr",
            BrowserOpfsEntryKind.File,
            10,
            20);
        var other = new BrowserOpfsEntrySnapshot(
            "settings.json",
            "settings.json",
            BrowserOpfsEntryKind.File,
            30,
            40);

        BrowserOpfsDownloadPlan plan = BrowserOpfsDownloadPlanner.Create(
            [child, other, folder],
            now);

        Assert.True(plan.UseZip);
        Assert.Equal(
            $"opfs-export-{now.ToLocalTime():yyyyMMdd-HHmmss}.zip",
            plan.SuggestedFileName);
        Assert.Equal(["charts", "settings.json"], plan.SelectedEntries.Select(x => x.RelativePath));
    }
}
