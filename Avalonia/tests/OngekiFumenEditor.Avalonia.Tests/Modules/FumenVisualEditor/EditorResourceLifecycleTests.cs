using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorResourceLifecycleTests
{
    [AvaloniaFact]
    public async Task Dispose_ReleasesRenderContextControlAudioAndGlobalSettingSubscription()
    {
        var editor = new FumenVisualEditorViewModel();
        var host = new ContentControl();
        var renderManager = new TrackingRenderManager();
        var audioPlayer = new TrackingAudioPlayer();
        editor.AudioPlayer = audioPlayer;
        var globalSetting = EditorGlobalSetting.Default;
        var originalOffset = globalSetting.JudgeLineOffsetY;
        var settingNotificationCount = 0;
        editor.Setting.PropertyChanged += OnSettingPropertyChanged;

        try
        {
            await editor.InitializeRenderControlAsync(
                host,
                renderManager,
                [],
                new DummyPerformenceMonitor());
            var renderControl = Assert.IsType<Panel>(host.Content);
            await editor.ActivateRenderControlAsync(renderControl, EventArgs.Empty);

            Assert.True(renderManager.Context.IsRendering);
            Assert.Equal(1, renderManager.Context.RenderSubscriberCount);
            Assert.Empty(editor.CurrentDrawingTargets);

            editor.Dispose();
            editor.Dispose();

            Assert.True(editor.IsDisposed);
            Assert.Null(host.Content);
            Assert.Null(editor.RenderContext);
            Assert.Empty(editor.CurrentDrawingTargets);
            Assert.False(renderManager.Context.IsRendering);
            Assert.Equal(0, renderManager.Context.RenderSubscriberCount);
            Assert.Equal(1, renderManager.Context.StopCount);
            Assert.Equal(1, renderManager.ReleaseCount);
            Assert.Same(renderControl, renderManager.ReleasedControl);
            Assert.Equal(1, audioPlayer.DisposeCount);

            globalSetting.JudgeLineOffsetY = originalOffset + 1;
            Assert.Equal(0, settingNotificationCount);
        }
        finally
        {
            editor.Setting.PropertyChanged -= OnSettingPropertyChanged;
            globalSetting.JudgeLineOffsetY = originalOffset;
            editor.Dispose();
        }

        void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorGlobalSetting.JudgeLineOffsetY))
                settingNotificationCount++;
        }
    }

    [AvaloniaFact]
    public void PlayerLocationHelper_Dispose_ReleasesLoadedTextureExactlyOnce()
    {
        var renderManager = new TrackingRenderManager();
        var helper = new DrawPlayerLocationHelper();

        helper.Initalize(renderManager);
        helper.Dispose();
        helper.Dispose();

        Assert.Equal(1, renderManager.Image.DisposeCount);
    }

    [AvaloniaFact]
    public async Task Dispose_DuringRenderInitialization_CancelsAttachmentAndReleasesControl()
    {
        var editor = new FumenVisualEditorViewModel();
        var host = new ContentControl();
        var renderManager = new TrackingRenderManager(delayInitialization: true);

        var initializationTask = editor.InitializeRenderControlAsync(
            host,
            renderManager,
            [],
            new DummyPerformenceMonitor());
        await renderManager.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        editor.Dispose();
        await initializationTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(editor.IsDisposed);
        Assert.Null(host.Content);
        Assert.Equal(1, renderManager.ReleaseCount);
        Assert.Equal(0, renderManager.Context.StartCount);
        Assert.Empty(editor.CurrentDrawingTargets);
        await editor.WaitForRenderInitializationIsDone().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [AvaloniaFact]
    public async Task Dispose_DuringNonCancelableInitialization_DoesNotReattachClosedEditor()
    {
        var editor = new FumenVisualEditorViewModel();
        var host = new ContentControl();
        var renderManager = new TrackingRenderManager(ignoreInitializationCancellation: true);

        var initializationTask = editor.InitializeRenderControlAsync(
            host,
            renderManager,
            [],
            new DummyPerformenceMonitor());
        await renderManager.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        editor.Dispose();
        renderManager.CompleteInitialization();
        await initializationTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(host.Content);
        Assert.Equal(1, renderManager.ReleaseCount);
        Assert.Equal(0, renderManager.Context.StartCount);
        Assert.Empty(editor.CurrentDrawingTargets);
    }

    [AvaloniaFact]
    public void DocumentManager_Destroy_UsesIdempotentEditorDisposal()
    {
        var scheduler = new StubSchedulerManager();
        var manager = new DefaultEditorDocumentManager(scheduler);
        var editor = new FumenVisualEditorViewModel()
        {
            AudioPlayer = new TrackingAudioPlayer()
        };
        var audioPlayer = (TrackingAudioPlayer)editor.AudioPlayer;

        try
        {
            manager.NotifyCreate(editor);
            manager.NotifyDestory(editor);
            manager.NotifyDestory(editor);

            Assert.True(editor.IsDisposed);
            Assert.Equal(1, audioPlayer.DisposeCount);
            Assert.DoesNotContain(editor, manager.GetCurrentEditors());
        }
        finally
        {
            manager.OnSchedulerTerm();
            editor.Dispose();
        }
    }

    [AvaloniaFact]
    public void DisposedEditor_IsCollectibleWhileReleasedControlRemainsAlive()
    {
        var retainedControl = CreateAndDisposeEditor(out var editorReference);

        CollectGarbage();

        Assert.NotNull(retainedControl);
        Assert.False(editorReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Panel CreateAndDisposeEditor(out WeakReference editorReference)
    {
        var editor = new FumenVisualEditorViewModel();
        var host = new ContentControl();
        var renderManager = new TrackingRenderManager();
        editor.InitializeRenderControlAsync(
            host,
            renderManager,
            [],
            new DummyPerformenceMonitor()).GetAwaiter().GetResult();
        var renderControl = Assert.IsType<Panel>(host.Content);
        editorReference = new WeakReference(editor);

        editor.Dispose();
        editor = null;
        host = null;
        renderManager = null;
        return renderControl;
    }

    private static void CollectGarbage()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private sealed class TrackingRenderManager(
        bool delayInitialization = false,
        bool ignoreInitializationCancellation = false) : IRenderManagerImpl
    {
        private readonly DefaultSkiaDrawingManagerImpl drawingManager = new();
        private readonly TaskCompletionSource initializationCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "Tracking";
        public TrackingRenderContext Context { get; } = new();
        public TrackingImage Image { get; } = new();
        public TaskCompletionSource InitializationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReleaseCount { get; private set; }
        public Control? ReleasedControl { get; private set; }

        public ICircleDrawing CircleDrawing => drawingManager.CircleDrawing;
        public ILineDrawing LineDrawing => drawingManager.LineDrawing;
        public ISimpleLineDrawing SimpleLineDrawing => drawingManager.SimpleLineDrawing;
        public IStaticVBODrawing StaticVBODrawing => drawingManager.StaticVBODrawing;
        public IStringDrawing StringDrawing => drawingManager.StringDrawing;
        public ITextureDrawing TextureDrawing => drawingManager.TextureDrawing;
        public IBatchTextureDrawing BatchTextureDrawing => drawingManager.BatchTextureDrawing;
        public IHighlightBatchTextureDrawing HighlightBatchTextureDrawing => drawingManager.HighlightBatchTextureDrawing;
        public IPolygonDrawing PolygonDrawing => drawingManager.PolygonDrawing;
        public IBeamDrawing BeamDrawing => drawingManager.BeamDrawing;
        public ISvgDrawing SvgDrawing => drawingManager.SvgDrawing;

        public Task WaitForInitializationIsDone(CancellationToken cancellation = default) => Task.CompletedTask;

        public async Task InitializeRenderControl(Control renderControl, CancellationToken cancellation = default)
        {
            InitializationStarted.TrySetResult();
            if (delayInitialization)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            else if (ignoreInitializationCancellation)
                await initializationCompletion.Task;
        }

        public void CompleteInitialization() => initializationCompletion.TrySetResult();

        public Task<IRenderContext> GetRenderContext(Control renderControl, CancellationToken cancellation = default) =>
            Task.FromResult<IRenderContext>(Context);

        public IImage LoadImageFromStream(Stream stream) => Image;

        public Control CreateRenderControl() => new Panel();

        public void ReleaseRenderControl(Control renderControl)
        {
            ReleaseCount++;
            ReleasedControl = renderControl;
            Context.StopRendering();
        }
    }

    private sealed class TrackingRenderContext : IRenderContext
    {
        private Action<TimeSpan>? render;

        public event Action<TimeSpan> OnRender
        {
            add => render += value;
            remove => render -= value;
        }

        public int RenderSubscriberCount => render?.GetInvocationList().Length ?? 0;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsRendering { get; private set; }

        public void BeforeRender(IDrawingContext context)
        {
        }

        public void AfterRender(IDrawingContext context)
        {
        }

        public void CleanRender(IDrawingContext context, Vector4 cleanColor)
        {
        }

        public void StartRendering()
        {
            StartCount++;
            IsRendering = true;
        }

        public void StopRendering()
        {
            if (!IsRendering)
                return;

            StopCount++;
            IsRendering = false;
        }
    }

    private sealed class TrackingImage : IImage
    {
        public int DisposeCount { get; private set; }
        public TextureWrapMode TextureWrapT { get; set; }
        public TextureWrapMode TextureWrapS { get; set; }

        public void Dispose() => DisposeCount++;
    }

    private sealed class TrackingAudioPlayer : IAudioPlayer
    {
        public TimeSpan CurrentTime => TimeSpan.Zero;
        public float Speed { get; set; } = 1;
        public TimeSpan Duration => TimeSpan.Zero;
        public bool IsPlaying => false;
        public bool IsAvaliable => true;
        public int DisposeCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public event IAudioPlayer.OnPlaybackFinishedFunc? OnPlaybackFinished
        {
            add { }
            remove { }
        }

        public void Play()
        {
        }

        public void Stop()
        {
        }

        public void Pause()
        {
        }

        public void Seek(TimeSpan timeSpan, bool pause)
        {
        }

        public Task<SampleData> GetSamplesAsync() => throw new NotSupportedException();

        public void Dispose() => DisposeCount++;
    }

    private sealed class StubSchedulerManager : ISchedulerManager
    {
        private readonly List<ISchedulable> schedulers = [];

        public IEnumerable<ISchedulable> Schedulers => schedulers;
        public Task Init() => Task.CompletedTask;

        public Task AddScheduler(ISchedulable schedulable)
        {
            if (!schedulers.Contains(schedulable))
                schedulers.Add(schedulable);
            return Task.CompletedTask;
        }

        public Task RemoveScheduler(ISchedulable schedulable)
        {
            schedulers.Remove(schedulable);
            return Task.CompletedTask;
        }

        public Task Term() => Task.CompletedTask;
    }
}
