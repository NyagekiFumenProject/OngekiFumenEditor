using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System.Numerics;
using Xunit;
using OngekiFumenEditor.Avalonia.Utils;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
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
    public void ContextSlots_PostSwapPresentRetainsFrontUntilSuperseded()
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
        // the front slot is retained after present and stays re-presentable
        Assert.False(presented.IsDisposed);
        Assert.False(slots.Swap(context));

        DrawCommandList? presentedAgain = null;
        slots.Present(context, list => presentedAgain = list);
        Assert.Same(presented, presentedAgain);

        // a newer frame supersedes and releases the retained one
        using var replacementBuilder = new DrawCommandListBuilder();
        replacementBuilder.DrawSimpleLines([Vertex(0, 0)], 1);
        slots.Post(context, replacementBuilder.GetDrawCommandList(), autoDispose: true);
        Assert.True(slots.Swap(context));
        Assert.True(presented.IsDisposed);

        // dropping the context releases the current frame
        Assert.True(slots.Remove(context));
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

    [Fact]
    public void Builder_ReusedPooledCommandsClearPreviousFrameState()
    {
        var firstTexture = new StubImage();
        var secondTexture = new StubImage();
        var firstFont = new StubFontHandle("first");
        var secondFont = new StubFontHandle("second");
        var firstTextureInstance = new TextureInstance(new Vector2(1, 2), new Vector2(3, 4), 5, Vector4.One);
        var secondTextureInstance = new TextureInstance(new Vector2(6, 7), new Vector2(8, 9), 10, Vector4.One);
        var firstCircleInstance = new CircleInstance(new Vector2(1, 2), Vector4.One, true, 3, 4);
        var secondCircleInstance = new CircleInstance(new Vector2(6, 7), Vector4.One, false, 8, 9);

        using var builder = new DrawCommandListBuilder();
        builder.PushModelMatrix(Matrix4.CreateTranslation(1, 2, 3));
        builder.DrawTexture(firstTexture, [firstTextureInstance]);
        builder.DrawCircles([firstCircleInstance]);
        builder.DrawString("first", Vector2.Zero, Vector2.One, 12, 0, Vector4.One, Vector2.Zero, IStringDrawing.StringStyle.Normal, firstFont);

        using (var first = builder.GetDrawCommandList())
            Assert.Equal(4, first.Commands.Count);

        Assert.Throws<InvalidOperationException>(builder.PopModelMatrix);

        builder.DrawTexture(secondTexture, [secondTextureInstance]);
        builder.DrawCircles([secondCircleInstance]);
        builder.DrawString("second", Vector2.One, Vector2.One, 14, 0, Vector4.One, Vector2.Zero, IStringDrawing.StringStyle.Bold, secondFont);

        using var second = builder.GetDrawCommandList();
        Assert.Equal(3, second.Commands.Count);

        var texture = Assert.Single(second.Commands.OfType<DrawTextureCommand>());
        Assert.Same(secondTexture, texture.Texture);
        Assert.Equal(new[] { secondTextureInstance }, texture.Instances);

        var circles = Assert.Single(second.Commands.OfType<DrawCirclesCommand>());
        Assert.Equal(new[] { secondCircleInstance }, circles.Instances);

        var text = Assert.Single(second.Commands.OfType<DrawStringCommand>());
        Assert.Equal("second", text.Text);
        Assert.Same(secondFont, text.FontHandle);
        Assert.Equal(IStringDrawing.StringStyle.Bold, text.Style);
    }

    [Fact]
    public void Builder_ExceptionDuringCircleValidation_ReleasesScratchCollection()
    {
        using var builder = new DrawCommandListBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.DrawCircles([new CircleInstance(Vector2.Zero, Vector4.One, true, -1, 0)]));
        using var scratch = ObjectPool.GetPooledList<CircleInstance>();
        Assert.Empty(scratch);

        builder.DrawCircles([new CircleInstance(Vector2.One, Vector4.One, false, 2, 0)]);
        using var list = builder.GetDrawCommandList();
        var command = Assert.Single(list.Commands.OfType<DrawCirclesCommand>());
        Assert.Equal(new Vector2(1, 1), Assert.Single(command.Instances).Point);
    }

    [Fact]
    public void ContextSlots_PresentException_RetainsFrontUntilContextRemoved()
    {
        var slots = new DrawCommandListContextSlots();
        var context = new StubRenderContext();
        using var builder = new DrawCommandListBuilder();
        builder.DrawSimpleLines([Vertex(0, 0)], 1);
        var list = builder.GetDrawCommandList();
        slots.Post(context, list, autoDispose: true);
        Assert.True(slots.Swap(context));

        Assert.Throws<InvalidOperationException>(() =>
            slots.Present(context, _ => throw new InvalidOperationException("present")));

        // a failed presentation keeps the frame retained instead of disposing it
        Assert.False(list.IsDisposed);
        Assert.False(slots.Swap(context));

        // the context slot owns the last reference and releases it on removal
        Assert.True(slots.Remove(context));
        Assert.True(list.IsDisposed);
    }

    private sealed class StubImage : IImage
    {
        public TextureWrapMode TextureWrapT { get; set; }

        public TextureWrapMode TextureWrapS { get; set; }

        public void Dispose()
        {
        }
    }

    private sealed class StubFontHandle(string familyName) : IStringDrawing.IFontHandle
    {
        public string FamilyName { get; } = familyName;
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
