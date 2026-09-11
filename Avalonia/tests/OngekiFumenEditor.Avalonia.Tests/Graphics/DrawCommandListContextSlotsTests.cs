using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Graphics;

/// <summary>
/// Pins the retained-frame contract used to avoid blank surfaces on throttled frames:
/// presenting keeps the front list re-presentable, and it is only released when a newer
/// frame supersedes it in Swap or when the context is dropped.
/// </summary>
public sealed class DrawCommandListContextSlotsTests
{
    [Fact]
    public void Present_KeepsFrontRePresentable_AndReleasesOnlyWhenSuperseded()
    {
        var slots = new DrawCommandListContextSlots();
        var context = new StubRenderContext();

        var first = CreateCommandList();
        slots.Post(context, first);
        Assert.True(slots.Swap(context));

        var presentCount = 0;
        slots.Present(context, _ => presentCount++);
        Assert.Equal(1, presentCount);
        Assert.False(first.IsDisposed);

        // Throttled frames keep presenting the retained front without a new Post/Swap.
        slots.Present(context, _ => presentCount++);
        slots.Present(context, _ => presentCount++);
        Assert.Equal(3, presentCount);
        Assert.False(first.IsDisposed);

        // A newer frame supersedes and releases the retained one at Swap time.
        var second = CreateCommandList();
        slots.Post(context, second);
        Assert.True(slots.Swap(context));
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);

        slots.Present(context, _ => presentCount++);
        Assert.Equal(4, presentCount);
        Assert.False(second.IsDisposed);

        // Dropping the context releases the last retained frame.
        Assert.True(slots.Remove(context));
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public void Present_WithoutAnyFrame_DoesNothing()
    {
        var slots = new DrawCommandListContextSlots();

        var presentCount = 0;
        slots.Present(new StubRenderContext(), _ => presentCount++);

        Assert.Equal(0, presentCount);
    }

    private static DrawCommandList CreateCommandList()
    {
        using var builder = new DrawCommandListBuilder();
        return builder.GetDrawCommandList();
    }

    private sealed class StubRenderContext : IRenderContext
    {
        public event Action<IRenderContext, TimeSpan> OnRender
        {
            add { }
            remove { }
        }

        public void PostDrawCommandList(DrawCommandList drawCommandList, bool autoDispose = true)
        {
        }

        public void StartRendering()
        {
        }

        public void StopRendering()
        {
        }
    }
}
