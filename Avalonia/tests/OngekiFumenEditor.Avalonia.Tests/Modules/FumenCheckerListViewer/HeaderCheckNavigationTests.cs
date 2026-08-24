using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenCheckerListViewer;

public sealed class HeaderCheckNavigationTests
{
    [AvaloniaFact]
    public async Task HeaderResults_NavigateToTheSharedMetaInfoToolForEveryMismatch()
    {
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();

        var editor = new FumenVisualEditorViewModel()
        {
            EditorContext = new EditorContext { Fumen = new OngekiFumen() }
        };
        editor.EditorContext.Fumen.MetaInfo.XRESOLUTION++;
        editor.EditorContext.Fumen.MetaInfo.TRESOLUTION++;
        editor.EditorContext.Fumen.MetaInfo.Creator = string.Empty;

        var rule = Assert.Single(
            IoC.GetAll<IFumenCheckRule>(),
            candidate => candidate.GetType().Name == "HeaderConstCheckRule");
        var results = rule.CheckRule(editor.EditorContext.Fumen, editor).ToArray();
        var tool = IoC.Get<IFumenMetaInfoBrowser>();

        Assert.Equal(3, results.Length);
        Assert.All(results, result => Assert.NotNull(result.NavigateBehavior));

        try
        {
            foreach (var result in results)
            {
                result.NavigateBehavior.Navigate(editor);

                Assert.Same(tool, Assert.Single(shell.Tools));
                Assert.Same(editor.EditorContext.Fumen, tool.Fumen);
            }
        }
        finally
        {
            shell.HideTool(tool);
        }
    }

    [AvaloniaFact]
    public async Task MetaInfoCommand_ReusesTheRegisteredInterfaceSingleton()
    {
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();
        var expectedTool = IoC.Get<IFumenMetaInfoBrowser>();
        var handler = Assert.Single(IoC.GetAll<ICommandHandler>()
            .OfType<OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands.ViewFumenMetaInfoBrowserCommandHandler>());

        Assert.IsType<FumenMetaInfoBrowserViewModel>(expectedTool);

        try
        {
            await handler.Run(new Command(
                new OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands.ViewFumenMetaInfoBrowserCommandDefinition()));
            await handler.Run(new Command(
                new OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands.ViewFumenMetaInfoBrowserCommandDefinition()));

            Assert.Same(expectedTool, Assert.Single(shell.Tools));
        }
        finally
        {
            shell.HideTool(expectedTool);
        }
    }
}
