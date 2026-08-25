using Xunit;
using System.Text.RegularExpressions;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.OgkiFumenListBrowser;

public sealed class OgkiFumenListBrowserStaticConstraintTests
{
    [Fact]
    public void ProductionModule_UsesSimpleFileCapabilitiesInsteadOfHostFileApis()
    {
        var moduleRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "OngekiFumenEditor.Avalonia", "Modules", "OgkiFumenListBrowser"));

        Assert.True(Directory.Exists(moduleRoot), moduleRoot);
        foreach (var file in Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("using System.IO", source, StringComparison.Ordinal);
            Assert.False(
                Regex.IsMatch(source, @"(?<![A-Za-z0-9_])(File|Directory|Path)\s*\."),
                file);
        }
    }
}
