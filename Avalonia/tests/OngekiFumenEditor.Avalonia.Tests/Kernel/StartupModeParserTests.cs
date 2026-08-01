using OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Kernel;

public sealed class StartupModeParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsGuiWithoutFile()
    {
        var options = StartupModeParser.Parse([]);

        Assert.Equal(StartupMode.Gui, options.Mode);
        Assert.Null(options.FilePath);
    }

    [Theory]
    [InlineData("--cmd")]
    [InlineData("--CMD")]
    [InlineData("--Cmd")]
    public void Parse_CmdSwitch_ReturnsCmd(string cmdSwitch)
    {
        var options = StartupModeParser.Parse([cmdSwitch]);

        Assert.Equal(StartupMode.Cmd, options.Mode);
        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_CmdSwitchMixedWithFile_CmdWins()
    {
        var existingFile = Path.GetTempFileName();
        try
        {
            var options = StartupModeParser.Parse(["--cmd", existingFile]);

            Assert.Equal(StartupMode.Cmd, options.Mode);
            Assert.Null(options.FilePath);
        }
        finally
        {
            File.Delete(existingFile);
        }
    }

    [Fact]
    public void Parse_SingleExistingFile_ReturnsGuiWithFile()
    {
        var existingFile = Path.GetTempFileName();
        try
        {
            var options = StartupModeParser.Parse([existingFile]);

            Assert.Equal(StartupMode.Gui, options.Mode);
            Assert.Equal(existingFile, options.FilePath);
        }
        finally
        {
            File.Delete(existingFile);
        }
    }

    [Fact]
    public void Parse_SingleMissingPath_ReturnsGuiWithoutFile()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ogkr");

        var options = StartupModeParser.Parse([missing]);

        Assert.Equal(StartupMode.Gui, options.Mode);
        Assert.Null(options.FilePath);
    }

    [Fact]
    public void Parse_MultipleNonSwitchArgs_ReturnsGuiWithoutFile()
    {
        var options = StartupModeParser.Parse(["a.ogkr", "b.ogkr"]);

        Assert.Equal(StartupMode.Gui, options.Mode);
        Assert.Null(options.FilePath);
    }
}
