using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// Pins the statistics lifecycle contract of <see cref="IPerfomenceMonitor"/>: the measurement
/// panel reads whatever the monitor holds, so a monitor must not keep showing samples from
/// rendering that already stopped or was replaced.
/// </summary>
public sealed class PerfomenceMonitorLifecycleTests
{
    [AvaloniaFact]
    public async Task StopRendering_DropsCollectedStatistics()
    {
        var (manager, control, context) = await CreateContextAsync();
        var monitor = new DefaultReleasePerfomenceMonitor();
        context.PerfomenceMonitor = monitor;
        SeedFrameAndPresent(monitor);
        Assert.Equal(1, monitor.GetRenderPerformenceData().AveDrawCall);

        context.StopRendering();

        var render = monitor.GetRenderPerformenceData();
        Assert.Equal(0, render.AveDrawCall);
        Assert.Equal(0, render.AvePresentSpendTicks);
        Assert.Equal(0, render.AveFrameSpendTicks);
        Assert.Equal(0, render.AveOnRenderSpendTicks);

        manager.ReleaseRenderControl(control);
    }

    [AvaloniaFact]
    public async Task InstallingMonitor_DropsSamplesFromItsPreviousAttachment()
    {
        var (manager, control, context) = await CreateContextAsync();
        var monitor = new DefaultReleasePerfomenceMonitor();
        SeedFrameAndPresent(monitor);

        context.PerfomenceMonitor = monitor;

        var render = monitor.GetRenderPerformenceData();
        Assert.Equal(0, render.AveDrawCall);
        Assert.Equal(0, render.AveFrameSpendTicks);

        manager.ReleaseRenderControl(control);
    }

    private static async Task<(DefaultSkiaDrawingManagerImpl Manager, Control Control, IRenderContext Context)> CreateContextAsync()
    {
        var manager = new DefaultSkiaDrawingManagerImpl();
        var control = manager.CreateRenderControl();
        await manager.InitializeRenderControl(control);
        var context = await manager.GetRenderContext(control);
        return (manager, control, context);
    }

    private static void SeedFrameAndPresent(DefaultReleasePerfomenceMonitor monitor)
    {
        // Two frame starts are needed: the interval sample is recorded when the next frame begins.
        monitor.OnBeforeRender();
        Thread.Sleep(2);
        monitor.OnAfterRender();
        monitor.OnBeforeRender();
        monitor.OnAfterRender();
        monitor.OnBeforePresent();
        monitor.CountDrawCall();
        monitor.OnAfterPresent();
    }
}
