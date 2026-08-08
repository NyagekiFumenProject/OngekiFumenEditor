using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer;
using OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;
using OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.TGridCalculatorToolViewer;

public sealed class TGridCalculatorCommandTests
{
    [AvaloniaFact]
    public async Task Command_ResolvesAndShowsTGridCalculatorOnce()
    {
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();
        var expectedTool = IoC.Get<ITGridCalculatorToolViewer>();
        var handler = Assert.Single(IoC.GetAll<ICommandHandler>()
            .OfType<ViewFumenMetaInfoBrowserCommandHandler>());

        Assert.IsType<TGridCalculatorToolViewerViewModel>(expectedTool);

        try
        {
            await handler.Run(new Command(new ViewTGridCalculatorToolViewerCommandDefinition()));

            Assert.Same(expectedTool, Assert.Single(shell.Tools));

            await handler.Run(new Command(new ViewTGridCalculatorToolViewerCommandDefinition()));

            Assert.Same(expectedTool, Assert.Single(shell.Tools));
        }
        finally
        {
            shell.HideTool(expectedTool);
        }
    }
}
