using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Commands;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenEditorSelectingObjectViewer;

public sealed class SelectingObjectViewerCommandTests
{
    [AvaloniaFact]
    public async Task Command_ResolvesAndShowsSelectingObjectViewerOnce()
    {
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();
        var expectedTool = IoC.Get<IFumenEditorSelectingObjectViewer>();
        var handler = Assert.Single(IoC.GetAll<ICommandHandler>()
            .OfType<ViewFumenMetaInfoBrowserCommandHandler>());

        Assert.IsType<FumenEditorSelectingObjectViewerViewModel>(expectedTool);

        try
        {
            await handler.Run(new Command(new ViewFumenEditorSelectingObjectViewerCommandDefinition()));

            Assert.Same(expectedTool, Assert.Single(shell.Tools));

            await handler.Run(new Command(new ViewFumenEditorSelectingObjectViewerCommandDefinition()));

            Assert.Same(expectedTool, Assert.Single(shell.Tools));
        }
        finally
        {
            shell.HideTool(expectedTool);
        }
    }
}
