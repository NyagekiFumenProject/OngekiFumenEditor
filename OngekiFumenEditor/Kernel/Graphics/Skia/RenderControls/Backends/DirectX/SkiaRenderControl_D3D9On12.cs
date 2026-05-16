using OngekiFumenEditor.Kernel.Graphics.Skia.D3dContexts;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Vortice.Direct3D9;
using Vortice.Direct3D9on12;
using Vortice.Direct3D12;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.RenderControls.Backends.DirectX
{
    internal class SkiaRenderControl_D3D9On12 : SkiaRenderControlBase
    {
        private const int MinFrameCount = 2;
        private const int MaxFrameCount = 5;

        // TODO: Remove global paint serialization after drawing implementations stop sharing mutable render state.
        private static readonly Lock PaintSurfaceLock = new();

        private readonly Lock threadLock = new();
        private readonly Lock resourceLock = new();
        private readonly Lock frameLock = new();
        private readonly Lock requestLock = new();

        private readonly D3DImage d3dImage = new();
        private readonly AutoResetEvent renderRequested = new(false);
        private readonly ManualResetEventSlim frameAvailable = new(false);

        private readonly List<IDirect3DSurface9> frames = new();
        private readonly List<IDirect3DSurface9> retiredFrames = new();
        private readonly Queue<IDirect3DSurface9> availableFrames = new();

        private VorticeDirect3DContext d3dContext;
        private GRD3DBackendContext d3dBackendContext;
        private GRContext grContext;
        private IDirect3D9Ex direct3D9Ex;
        private IDirect3DDevice9Ex direct3DDevice9Ex;
        private IDirect3DDevice9On12 direct3DDevice9On12;
        private ID3D12Fence presentFence;

        private Thread renderThread;
        private volatile bool renderThreadRunning;

        private ulong presentFenceValue;
        private int backBufferWidth;
        private int backBufferHeight;
        private int backBufferFrameCount;
        private int presentDispatchPending;
        private bool hasBackBuffer;
        private IDirect3DSurface9 displayedFrame;
        private RenderedFrame latestRenderedFrame;
        private IntPtr mainWindowHandle;
        private RenderRequest pendingRequest;

        private sealed class RenderRequest
        {
            public SKImageInfo ImageInfo { get; init; }
            public SKSizeI CanvasSize { get; init; }
            public SKSizeI OutputSize { get; init; }
            public float ScaleX { get; init; }
            public float ScaleY { get; init; }
            public bool IgnorePixelScaling { get; init; }
        }

        private sealed class RenderedFrame
        {
            public RenderedFrame(IDirect3DSurface9 surface, SKImageInfo imageInfo, SKSizeI outputSize)
            {
                Surface = surface;
                ImageInfo = imageInfo;
                OutputSize = outputSize;
            }

            public IDirect3DSurface9 Surface { get; }
            public SKImageInfo ImageInfo { get; }
            public SKSizeI OutputSize { get; }
        }

        public SkiaRenderControl_D3D9On12()
        {
            mainWindowHandle = new WindowInteropHelper(Application.Current?.MainWindow).Handle;

            Loaded += (_, _) => StartRenderThread();
            Unloaded += (_, _) => DetachBackBuffer();
            Dispatcher.ShutdownStarted += (_, _) => ReleaseGraphicsPipeline();
            d3dImage.IsFrontBufferAvailableChanged += (_, _) =>
            {
                if (d3dImage.IsFrontBufferAvailable)
                    SchedulePresentLatestFrame();

                InvalidateVisual();
            };

            if (!designMode)
                StartRenderThread();
        }

        private void StartRenderThread()
        {
            if (designMode)
                return;

            lock (threadLock)
            {
                if (renderThread?.IsAlive == true)
                    return;

                renderThreadRunning = true;
                renderThread = new Thread(RenderLoop)
                {
                    IsBackground = true,
                    Name = "SkiaRenderControl_D3D9On12 Render Thread"
                };
                renderThread.Start();
            }
        }

        private void RenderLoop()
        {
            try
            {
                while (renderThreadRunning)
                {
                    renderRequested.WaitOne();
                    if (!renderThreadRunning)
                        break;

                    do
                    {
                        RenderOneFrame();
                    }
                    while (renderThreadRunning && renderRequested.WaitOne(0));
                }
            }
            catch (Exception e)
            {
                Log.LogError($"D3D9On12 render thread stopped unexpectedly: {e}");
            }
            finally
            {
                lock (threadLock)
                {
                    if (ReferenceEquals(renderThread, Thread.CurrentThread))
                        renderThread = null;
                }
            }
        }

        private void RenderOneFrame()
        {
            var request = GetPendingRequest();
            if (!IsValidRequest(request))
                return;

            IDirect3DSurface9 renderSurface9 = null;
            var surfaceConsumed = false;

            try
            {
                if (!TryEnsureSharedResources())
                    return;

                EnsureBackBuffer(request.ImageInfo.Width, request.ImageInfo.Height);
                renderSurface9 = WaitForAvailableFrame();
                if (renderSurface9 is null)
                    return;

                var latestRequest = GetPendingRequest();
                if (!IsValidRequest(latestRequest))
                    return;

                if (latestRequest.ImageInfo.Width != request.ImageInfo.Width || latestRequest.ImageInfo.Height != request.ImageInfo.Height)
                {
                    ReturnAvailableFrame(renderSurface9);
                    renderSurface9 = null;
                    renderRequested.Set();
                    return;
                }

                request = latestRequest;
                if (RenderFrame(renderSurface9, request))
                {
                    surfaceConsumed = true;
                    EnqueueLatestRenderedFrame(new RenderedFrame(renderSurface9, request.ImageInfo, request.OutputSize));
                    SchedulePresentLatestFrame();
                }
            }
            catch (Exception e)
            {
                Log.LogError($"D3D9On12 failed to render a frame. The graphics pipeline will be rebuilt. {e}");
                ResetGraphicsPipeline();
                surfaceConsumed = true;
            }
            finally
            {
                if (renderSurface9 is not null && !surfaceConsumed)
                    ReturnAvailableFrame(renderSurface9);
            }
        }

        private IDirect3DSurface9 WaitForAvailableFrame()
        {
            while (renderThreadRunning)
            {
                if (TryTakeAvailableFrame(out var frame))
                    return frame;

                SchedulePresentLatestFrame();
                frameAvailable.Wait(16);
            }

            return null;
        }

        private static bool IsValidRequest(RenderRequest request) =>
            request is not null && request.ImageInfo.Width > 0 && request.ImageInfo.Height > 0;

        private RenderRequest GetPendingRequest()
        {
            lock (requestLock)
                return pendingRequest;
        }

        private void SetPendingRequest(RenderRequest request)
        {
            lock (requestLock)
                pendingRequest = request;
        }

        private void SchedulePresentLatestFrame()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            if (Interlocked.Exchange(ref presentDispatchPending, 1) == 1)
                return;

            Dispatcher.BeginInvoke(new Action(PresentLatestFrameOnDispatcher), DispatcherPriority.Render);
        }

        private void PresentLatestFrameOnDispatcher()
        {
            Interlocked.Exchange(ref presentDispatchPending, 0);

            if (PresentLatestRenderedFrame())
                InvalidateVisual();
        }

        private bool TryEnsureSharedResources()
        {
            lock (resourceLock)
            {
                if (grContext is not null && direct3DDevice9On12 is not null && direct3DDevice9Ex is not null)
                    return true;

                try
                {
                    var hwnd = EnsureMainWindowHandle();
                    if (hwnd == IntPtr.Zero)
                        return false;

                    d3dContext ??= new VorticeDirect3DContext();
                    d3dBackendContext ??= d3dContext.CreateBackendContext();
                    grContext ??= GRContext.CreateDirect3D(d3dBackendContext);

                    if (direct3DDevice9On12 is not null && direct3DDevice9Ex is not null)
                        return true;

                    var adapterLuid = d3dContext.Adapter.Description1.Luid;
                    var d3d9On12Args = new[]
                    {
                        new D3D9On12Arguments
                        {
                            Enable9On12 = true,
                            D3D12Device = d3dContext.Device,
                            D3D12Queue1 = d3dContext.Queue,
                            NodeMask = 0,
                        }
                    };

                    direct3D9Ex = Apis.Direct3DCreate9On12Ex(d3d9On12Args);

                    uint adapterIndex = 0;
                    for (uint i = 0; i < direct3D9Ex.AdapterCount; i++)
                    {
                        var luid = direct3D9Ex.GetAdapterLuid(i);
                        if (luid.LowPart == adapterLuid.LowPart && luid.HighPart == adapterLuid.HighPart)
                        {
                            adapterIndex = i;
                            break;
                        }
                    }

                    var presentParameters = new PresentParameters
                    {
                        BackBufferWidth = 1,
                        BackBufferHeight = 1,
                        BackBufferFormat = Vortice.Direct3D9.Format.Unknown,
                        BackBufferCount = 1,
                        MultiSampleType = MultisampleType.None,
                        MultiSampleQuality = 0,
                        SwapEffect = SwapEffect.Discard,
                        DeviceWindowHandle = hwnd,
                        Windowed = true,
                        EnableAutoDepthStencil = false,
                        PresentationInterval = PresentInterval.Immediate,
                    };

                    var createFlags = CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve;
                    direct3DDevice9Ex = direct3D9Ex.CreateDeviceEx(adapterIndex, DeviceType.Hardware, hwnd, createFlags, presentParameters);
                    direct3DDevice9On12 = direct3DDevice9Ex.QueryInterface<IDirect3DDevice9On12>();
                    presentFence ??= d3dContext.Device.CreateFence(0);

                    Log.LogInfo($"Initialized D3D9On12 presentation device. adapterIndex={adapterIndex}, luid=0x{adapterLuid.HighPart:X8}{adapterLuid.LowPart:X8}");
                    return true;
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to initialize D3D9On12 graphics resources: {e}");
                    ReleaseSharedResourcesLocked();
                    return false;
                }
            }
        }

        private IntPtr EnsureMainWindowHandle()
        {
            if (mainWindowHandle != IntPtr.Zero)
                return mainWindowHandle;

            if (Dispatcher.CheckAccess())
                return mainWindowHandle = new WindowInteropHelper(Application.Current?.MainWindow).Handle;

            return mainWindowHandle = Dispatcher.Invoke(() => new WindowInteropHelper(Application.Current?.MainWindow).Handle);
        }

        private bool RenderFrame(IDirect3DSurface9 renderSurface9, RenderRequest request)
        {
            using var renderTexture12 = direct3DDevice9On12.UnwrapUnderlyingResource<ID3D12Resource>(renderSurface9, d3dContext.Queue);
            var returnedUnderlyingResource = false;

            try
            {
                using var backendRenderTarget = new GRBackendRenderTarget(request.ImageInfo.Width, request.ImageInfo.Height,
                    new GRVorticeD3DTextureResourceInfo
                    {
                        Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
                        Resource = renderTexture12,
                        ResourceState = ResourceStates.RenderTarget,
                        SampleCount = 1,
                        LevelCount = 1,
                        SampleQualityPattern = 0,
                        Protected = false,
                    });

                using var renderSurface = SKSurface.Create(grContext, backendRenderTarget, GRSurfaceOrigin.TopLeft, request.ImageInfo.ColorType);
                if (renderSurface is null)
                {
                    ReturnUnderlyingResource(renderSurface9);
                    returnedUnderlyingResource = true;
                    return false;
                }

                if (!PaintFrame(renderSurface, request))
                {
                    ReturnUnderlyingResource(renderSurface9);
                    returnedUnderlyingResource = true;
                    return false;
                }

                renderSurface.Flush(true, true);
                grContext.Flush(true, true);
                grContext.Submit(true);

                var signalValue = ++presentFenceValue;
                d3dContext.Queue.Signal(presentFence, signalValue);
                direct3DDevice9On12.ReturnUnderlyingResource(renderSurface9, [signalValue], [presentFence]);
                returnedUnderlyingResource = true;
                return true;
            }
            finally
            {
                if (!returnedUnderlyingResource)
                    ReturnUnderlyingResource(renderSurface9);
            }
        }

        private bool PaintFrame(SKSurface renderSurface, RenderRequest request)
        {
            lock (PaintSurfaceLock)
            {
                var canvasSaved = false;
                try
                {
                    if (!request.IgnorePixelScaling)
                    {
                        renderSurface.Canvas.Save();
                        canvasSaved = true;
                        renderSurface.Canvas.Scale(request.ScaleX, request.ScaleY);
                    }

                    SetCurrentRenderSurface(renderSurface, false);
                    OnPaintSurface(new SKPaintSurfaceEventArgs(renderSurface, request.ImageInfo.WithSize(request.CanvasSize), request.ImageInfo));
                    return true;
                }
                catch (Exception e)
                {
                    Log.LogError($"D3D9On12 paint callback failed: {e}");
                    return false;
                }
                finally
                {
                    SetCurrentRenderSurface(default, false);
                    if (canvasSaved)
                        renderSurface.Canvas.Restore();
                }
            }
        }

        private void ReturnUnderlyingResource(IDirect3DSurface9 renderSurface9)
        {
            try
            {
                direct3DDevice9On12?.ReturnUnderlyingResource(renderSurface9, [], []);
            }
            catch (Exception e)
            {
                Log.LogWarn($"Failed to return D3D9On12 underlying resource: {e}");
            }
        }

        private void EnsureBackBuffer(int width, int height)
        {
            var frameCount = GetRenderQueueFrameCount();
            lock (frameLock)
            {
                if (frames.Count > 0 && backBufferWidth == width && backBufferHeight == height && backBufferFrameCount == frameCount)
                    return;
            }

            ReleaseBackBufferSurfaces(false);

            var newFrames = new List<IDirect3DSurface9>(frameCount);
            for (var i = 0; i < frameCount; i++)
            {
                newFrames.Add(direct3DDevice9Ex.CreateRenderTargetEx(
                    (uint)width,
                    (uint)height,
                    Vortice.Direct3D9.Format.A8R8G8B8,
                    MultisampleType.None,
                    0,
                    false,
                    Usage.None));
            }

            lock (frameLock)
            {
                frames.AddRange(newFrames);
                foreach (var frame in newFrames)
                    availableFrames.Enqueue(frame);

                backBufferWidth = width;
                backBufferHeight = height;
                backBufferFrameCount = frameCount;
            }

            frameAvailable.Set();
        }

        private static int GetRenderQueueFrameCount() =>
            Math.Clamp(ProgramSetting.Default.D3DRenderQueueFrameCount, MinFrameCount, MaxFrameCount);

        private bool TryTakeAvailableFrame(out IDirect3DSurface9 frame)
        {
            lock (frameLock)
            {
                if (availableFrames.Count == 0)
                {
                    frame = null;
                    frameAvailable.Reset();
                    return false;
                }

                frame = availableFrames.Dequeue();
                if (availableFrames.Count == 0)
                    frameAvailable.Reset();
                return true;
            }
        }

        private void ReturnAvailableFrame(IDirect3DSurface9 frame)
        {
            lock (frameLock)
                ReturnAvailableFrameLocked(frame);
        }

        private void EnqueueLatestRenderedFrame(RenderedFrame frame)
        {
            lock (frameLock)
            {
                if (!frames.Contains(frame.Surface))
                {
                    RetireFrameLocked(frame.Surface);
                    return;
                }

                if (latestRenderedFrame is not null)
                    ReturnAvailableFrameLocked(latestRenderedFrame.Surface);

                latestRenderedFrame = frame;
            }
        }

        private void ReturnAvailableFrameLocked(IDirect3DSurface9 frame)
        {
            if (frame is null)
                return;

            if (frames.Contains(frame) && frame != displayedFrame)
            {
                availableFrames.Enqueue(frame);
                frameAvailable.Set();
            }
            else
            {
                RetireFrameLocked(frame);
            }
        }

        private void DetachBackBuffer()
        {
            if (!d3dImage.IsFrontBufferAvailable && !hasBackBuffer)
                return;

            d3dImage.Lock();
            try
            {
                d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);

                hasBackBuffer = false;
                lock (frameLock)
                {
                    var previousDisplayedFrame = displayedFrame;
                    displayedFrame = null;
                    ReturnAvailableFrameLocked(previousDisplayedFrame);
                    DisposeRetiredFramesExceptLocked(null);
                }
            }
            finally
            {
                d3dImage.Unlock();
            }
        }

        private void ReleaseBackBufferSurfaces(bool forgetDisplayedFrame)
        {
            lock (frameLock)
            {
                foreach (var frame in frames)
                    RetireFrameLocked(frame);

                if (forgetDisplayedFrame)
                {
                    RetireFrameLocked(displayedFrame);
                    displayedFrame = null;
                }

                frames.Clear();
                availableFrames.Clear();
                latestRenderedFrame = null;
                backBufferWidth = 0;
                backBufferHeight = 0;
                backBufferFrameCount = 0;

                DisposeRetiredFramesExceptLocked(displayedFrame);
            }

            frameAvailable.Set();
        }

        private void ResetGraphicsPipeline()
        {
            DetachBackBufferOnDispatcher();
            ReleaseBackBufferSurfaces(true);
            ReleaseSharedResources();
        }

        private void DetachBackBufferOnDispatcher()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            try
            {
                if (Dispatcher.CheckAccess())
                    DetachBackBuffer();
                else
                    Dispatcher.Invoke(DetachBackBuffer);
            }
            catch (Exception e)
            {
                Log.LogWarn($"Failed to detach D3DImage back buffer: {e}");
            }
        }

        private void ReleaseGraphicsPipeline()
        {
            DetachBackBuffer();
            if (StopRenderThread())
            {
                ReleaseBackBufferSurfaces(true);
                ReleaseSharedResources();
            }
        }

        private bool StopRenderThread()
        {
            Thread thread;
            lock (threadLock)
            {
                if (renderThread is null)
                    return true;

                renderThreadRunning = false;
                frameAvailable.Set();
                renderRequested.Set();
                thread = renderThread;
            }

            if (Thread.CurrentThread != thread && !thread.Join(TimeSpan.FromSeconds(1)))
            {
                Log.LogWarn("D3D9On12 render thread did not stop within 1 second.");
                return false;
            }

            lock (threadLock)
            {
                if (ReferenceEquals(renderThread, thread))
                    renderThread = null;
            }
            return true;
        }

        private void ReleaseSharedResources()
        {
            lock (resourceLock)
                ReleaseSharedResourcesLocked();
        }

        private void ReleaseSharedResourcesLocked()
        {
            presentFence?.Dispose();
            presentFence = null;
            direct3DDevice9On12?.Dispose();
            direct3DDevice9On12 = null;
            direct3DDevice9Ex?.Dispose();
            direct3DDevice9Ex = null;
            direct3D9Ex?.Dispose();
            direct3D9Ex = null;
            grContext?.Dispose();
            grContext = null;
            d3dContext?.Dispose();
            d3dContext = null;
            d3dBackendContext = null;
            presentFenceValue = 0;

            lock (frameLock)
            {
                foreach (var frame in retiredFrames)
                    frame.Dispose();

                retiredFrames.Clear();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (designMode)
                return;

            if (Visibility != Visibility.Visible || PresentationSource.FromVisual(this) == null)
                return;

            var pixelSize = CreateD3DSize(out var outputSize, out var scaleX, out var scaleY);
            CanvasSize = outputSize;

            SetPendingRequest(new RenderRequest
            {
                ImageInfo = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul),
                CanvasSize = outputSize,
                OutputSize = outputSize,
                ScaleX = scaleX,
                ScaleY = scaleY,
                IgnorePixelScaling = IgnorePixelScaling,
            });

            if (!d3dImage.IsFrontBufferAvailable || pixelSize.Width <= 0 || pixelSize.Height <= 0)
                return;

            PresentLatestRenderedFrame();

            if (hasBackBuffer)
                drawingContext.DrawImage(d3dImage, new System.Windows.Rect(0, 0, outputSize.Width, outputSize.Height));

            renderRequested.Set();
        }

        private SKSizeI CreateD3DSize(out SKSizeI outputSize, out float scaleX, out float scaleY)
        {
            if (!IsPositive(ActualWidth) || !IsPositive(ActualHeight))
            {
                outputSize = SKSizeI.Empty;
                scaleX = 1;
                scaleY = 1;
                return SKSizeI.Empty;
            }

            outputSize = new SKSizeI(
                Math.Max(1, (int)Math.Ceiling(ActualWidth)),
                Math.Max(1, (int)Math.Ceiling(ActualHeight)));

            if (IgnorePixelScaling)
            {
                scaleX = 1;
                scaleY = 1;
                return outputSize;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            scaleX = (float)dpi.DpiScaleX;
            scaleY = (float)dpi.DpiScaleY;
            return new SKSizeI(
                Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX)),
                Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY)));

            static bool IsPositive(double value) =>
                !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }

        private bool PresentLatestRenderedFrame()
        {
            if (!d3dImage.IsFrontBufferAvailable)
                return false;

            var presented = false;
            d3dImage.Lock();
            try
            {
                lock (frameLock)
                {
                    var frame = latestRenderedFrame;
                    if (frame is null)
                        return false;

                    latestRenderedFrame = null;
                    if (!frames.Contains(frame.Surface))
                    {
                        RetireFrameLocked(frame.Surface);
                        DisposeRetiredFramesExceptLocked(displayedFrame);
                        return false;
                    }

                    var previousDisplayedFrame = displayedFrame;
                    d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, frame.Surface.NativePointer, true);

                    var dirtyWidth = Math.Min(frame.ImageInfo.Width, d3dImage.PixelWidth);
                    var dirtyHeight = Math.Min(frame.ImageInfo.Height, d3dImage.PixelHeight);
                    if (dirtyWidth > 0 && dirtyHeight > 0)
                        d3dImage.AddDirtyRect(new Int32Rect(0, 0, dirtyWidth, dirtyHeight));

                    displayedFrame = frame.Surface;
                    hasBackBuffer = true;
                    presented = true;

                    ReturnAvailableFrameLocked(previousDisplayedFrame);
                    DisposeRetiredFramesExceptLocked(displayedFrame);
                }
            }
            finally
            {
                d3dImage.Unlock();
            }

            return presented;
        }

        private void RetireFrameLocked(IDirect3DSurface9 frame)
        {
            if (frame is null || retiredFrames.Contains(frame))
                return;

            retiredFrames.Add(frame);
        }

        private void DisposeRetiredFramesExceptLocked(IDirect3DSurface9 frameToKeep)
        {
            for (var i = retiredFrames.Count - 1; i >= 0; i--)
            {
                var frame = retiredFrames[i];
                if (frame == frameToKeep)
                    continue;

                retiredFrames.RemoveAt(i);
                frame.Dispose();
            }
        }
    }
}
