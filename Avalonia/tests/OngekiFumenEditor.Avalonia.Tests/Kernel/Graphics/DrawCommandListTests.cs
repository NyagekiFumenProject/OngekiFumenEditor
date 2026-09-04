using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System.Numerics;
using Xunit;
using OngekiFumenEditor.Avalonia.Utils;
using Matrix4 = OpenTK.Mathematics.Matrix4;
namespace OngekiFumenEditor.Avalonia.Tests.Kernel.Graphics;

public sealed class DrawCommandListTests
{
    public DrawCommandListTests()
    {
        // ObjectPool registration logs through the IoC-backed Log; keep these pure unit tests DI-free
        Log.Initialize(new Log([]));
    }

    private static ILineDrawing.LineVertex Vertex(float x, float y) => new(new Vector2(x, y), Vector4.One, ILineDrawing.VertexDash.Solider);

    [Fact]
    public void Builder_DeduplicatesEqualConsecutiveStateCommands()
    {
        using var builder = new DrawCommandListBuilder();
        var matrix = Matrix4.Identity;

        builder.SetCurrentViewMatrix(matrix);
        builder.SetCurrentViewMatrix(matrix);

        using var list = builder.GetDrawCommandList();

        Assert.Equal(1, list.Commands.Count);
        Assert.IsType<SetCurrentViewMatrixCommand>(list.Commands[0]);
    }

    [Fact]
    public void Builder_ReplacesAdjacentSameTypeStateCommand()
    {
        using var builder = new DrawCommandListBuilder();

        builder.SetCurrentViewMatrix(Matrix4.Identity);
        builder.SetCurrentViewMatrix(Matrix4.CreateTranslation(1, 2, 3));

        using var list = builder.GetDrawCommandList();

        Assert.Equal(1, list.Commands.Count);
        var command = Assert.IsType<SetCurrentViewMatrixCommand>(list.Commands[0]);
        Assert.Equal(Matrix4.CreateTranslation(1, 2, 3), command.Matrix);
    }

    [Fact]
    public void Builder_DifferentDrawCommandsAreKeptSeparately()
    {
        using var builder = new DrawCommandListBuilder();

        builder.DrawSimpleLines([Vertex(0, 0), Vertex(1, 1)], 1);
        builder.DrawSimpleLines([Vertex(2, 2), Vertex(3, 3)], 1);

        using var list = builder.GetDrawCommandList();

        Assert.Equal(2, list.Commands.Count);
    }

    [Fact]
    public void Builder_GetDrawCommandListResetsState()
    {
        using var builder = new DrawCommandListBuilder();

        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        using var first = builder.GetDrawCommandList();
        using var second = builder.GetDrawCommandList();

        Assert.Equal(1, first.Commands.Count);
        Assert.Empty(second.Commands);
    }

    [Fact]
    public void Builder_SnapshotIsImmutableAfterFurtherRecording()
    {
        using var builder = new DrawCommandListBuilder();

        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        using var snapshot = builder.GetDrawCommandList();
        builder.DrawSimpleLines([Vertex(1, 1)], 1);
        builder.Clear();

        Assert.Equal(1, snapshot.Commands.Count);
    }

    [Fact]
    public void Builder_SkipsEmptyCollections()
    {
        using var builder = new DrawCommandListBuilder();

        builder.DrawSimpleLines([], 1);

        using var list = builder.GetDrawCommandList();
        Assert.Empty(list.Commands);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Builder_RejectsNonPositiveLineWidth(float lineWidth)
    {
        using var builder = new DrawCommandListBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.DrawSimpleLines([Vertex(0, 0)], lineWidth));
    }

    [Fact]
    public void Builder_PopWithoutPushThrows()
    {
        using var builder = new DrawCommandListBuilder();

        Assert.Throws<InvalidOperationException>(builder.PopViewMatrix);
    }

    [Fact]
    public void Builder_ClearResetsPendingCommands()
    {
        using var builder = new DrawCommandListBuilder();

        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        builder.Clear();

        using var list = builder.GetDrawCommandList();
        Assert.Empty(list.Commands);
    }
    [Fact]
    public void CommandList_DisposeIsIdempotentAndClearsCommands()
    {
        using var builder = new DrawCommandListBuilder();
        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        var list = builder.GetDrawCommandList();

        Assert.NotEmpty(list.Commands);
        list.Dispose();
        Assert.Empty(list.Commands);
        Assert.True(list.IsDisposed);

        list.Dispose();
        Assert.Empty(list.Commands);
    }

    [Fact]
    public void ContextSlots_PostSwapPresentCycleClearsFront()
    {
        var slots = new DrawCommandListContextSlots();
        var context = new StubRenderContext();

        DrawCommandList? presented = null;
        using (var builder = new DrawCommandListBuilder())
        {
            builder.DrawSimpleLines([Vertex(0, 0)], 1);
            slots.Post(context, builder.GetDrawCommandList(), autoDispose: true);

            Assert.True(slots.Swap(context));

            slots.Present(context, list => presented = list);
        }

        Assert.NotNull(presented);
        // front slot was cleared and auto-disposed after present
        Assert.True(presented.IsDisposed);
        // second swap without a new post fails
        Assert.False(slots.Swap(context));
    }

    [Fact]
    public void ContextSlots_SwapWithEmptyBackReturnsFalse()
    {
        var slots = new DrawCommandListContextSlots();
        var context = new StubRenderContext();

        Assert.False(slots.Swap(context));
    }

    [Fact]
    public void ContextSlots_PresentSkipsDisposedList()
    {
        var slots = new DrawCommandListContextSlots();
        var context = new StubRenderContext();
        DrawCommandList? presented = null;

        using var builder = new DrawCommandListBuilder();
        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        var list = builder.GetDrawCommandList();
        slots.Post(context, list, autoDispose: false);
        list.Dispose();

        slots.Swap(context);
        slots.Present(context, l => presented = l);

        Assert.Null(presented);
    }

    private sealed class StubRenderContext : IRenderContext
    {
        public event Action<IRenderContext, TimeSpan>? OnRender;

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
