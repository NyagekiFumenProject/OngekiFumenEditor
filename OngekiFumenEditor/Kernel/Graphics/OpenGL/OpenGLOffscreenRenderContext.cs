using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OpenTK.Graphics.OpenGL;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    /// <summary>
    /// OpenGL 离屏渲染上下文：一个 FBO + 每次渲染新建的一张颜色纹理（零拷贝交给调用方）。
    /// 渲染由 <see cref="DefaultOpenGLRenderManagerImpl.PumpOffscreenRenders"/> 在 GL 控件的渲染回调（上下文 current）内排空执行，
    /// 因此要求存在处于 <c>StartRendering</c> 且可见的 GL 控件；控件不可见时请求会保持挂起，直到重新可见或被 Dispose/Term 结束。
    /// </summary>
    internal sealed class OpenGLOffscreenRenderContext : IOffscreenRenderContext
    {
        private readonly DefaultOpenGLRenderManagerImpl manager;
        private readonly OpenGLOffscreenRenderQueue queue;
        private readonly OpenGLOffscreenReplayContextAdapter replayContext;
        private readonly object stateGate = new();
        private bool disposed;
        private int framebufferId;

        public OpenGLOffscreenRenderContext(DefaultOpenGLRenderManagerImpl manager, OpenGLOffscreenRenderQueue queue, OffscreenRenderOptions options)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            Options = options ?? throw new ArgumentNullException(nameof(options));

            replayContext = new OpenGLOffscreenReplayContextAdapter(this);
        }

        /// <inheritdoc />
        public OffscreenRenderOptions Options { get; }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get
            {
                lock (stateGate)
                    return disposed;
            }
        }

        /// <inheritdoc />
        public Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true, CancellationToken cancellationToken = default)
        {
            if (drawCommandList is null)
                throw new ArgumentNullException(nameof(drawCommandList));

            if (cancellationToken.IsCancellationRequested)
            {
                if (autoDispose)
                    drawCommandList.Dispose();

                return Task.FromCanceled<IImage>(cancellationToken);
            }

            lock (stateGate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(OpenGLOffscreenRenderContext));

                if (drawCommandList.IsDisposed)
                    throw new ObjectDisposedException(nameof(drawCommandList));

                if (!drawCommandList.TryBeginPresent())
                    throw new InvalidOperationException("命令列表正在被其它渲染流程使用，或已进入释放流程。");

                var request = new OpenGLOffscreenRenderRequest(this, drawCommandList, autoDispose, cancellationToken);
                try
                {
                    queue.Submit(request);
                }
                catch
                {
                    if (autoDispose)
                        drawCommandList.Dispose();

                    drawCommandList.EndPresent();

                    throw;
                }

                return request.Task;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (stateGate)
            {
                if (disposed)
                    return;

                disposed = true;

                var fbo = framebufferId;
                framebufferId = 0;
                if (fbo != 0)
                    manager.EnqueueFramebufferDeletion(fbo);
            }

            // 与 Skia 不同：OpenGL 的推进依赖 UI tick，不能假设它继续 tick，因此取消尚未开始的排队请求；
            // 正在 drain 中的请求会照常完成并把结果图像交给调用方。
            queue.CancelQueued(this, new ObjectDisposedException(nameof(OpenGLOffscreenRenderContext), "离屏渲染上下文已释放，未开始的渲染请求被取消。"));
        }

        /// <summary>在 drain（GL 上下文 current）内执行一次渲染。</summary>
        internal IImage RenderCore(DefaultOpenGLRenderManagerImpl managerImpl, DrawCommandList drawCommandList)
        {
            Options.EnsureViewportMatches(drawCommandList.FrameState);

            var textureId = CreateRenderTexture();
            GL.GetInteger(GetPName.DrawFramebufferBinding, out var prevDrawFramebuffer);
            GL.GetInteger(GetPName.ReadFramebufferBinding, out var prevReadFramebuffer);
            var prevViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, prevViewport);

            try
            {
                var fbo = GetOrCreateFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureId, 0);

                var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (status != FramebufferErrorCode.FramebufferComplete)
                    throw new NotSupportedException($"离屏帧缓冲不完整({status})：像素格式 {Options.PixelFormat} 可能不被当前驱动支持。");

                // flipY：把 GL 纹理行序（t=0 在底部）对齐到 Bitmap/Skia 图像行序，避免结果贴回时上下颠倒。
                OpenGLDrawCommandListReplay.Present(managerImpl, replayContext, drawCommandList, flipY: true);

                return new DefaultOpenGLTexture(textureId, Options.Width, Options.Height, managerImpl.EnqueueTextureDeletion, "OffscreenTexture");
            }
            catch
            {
                managerImpl.EnqueueTextureDeletion(textureId);
                throw;
            }
            finally
            {
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, prevDrawFramebuffer);
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevReadFramebuffer);
                GL.Viewport(prevViewport[0], prevViewport[1], prevViewport[2], prevViewport[3]);
                OpenGLTextureBindingCache.Reset();

                if (IsDisposed)
                    ReleaseFramebuffer();
            }
        }

        private int GetOrCreateFramebuffer()
        {
            lock (stateGate)
            {
                if (framebufferId != 0)
                    return framebufferId;

                GL.GenFramebuffers(1, out int fbo);
                framebufferId = fbo;
                return fbo;
            }
        }

        private void ReleaseFramebuffer()
        {
            int fbo;
            lock (stateGate)
            {
                fbo = framebufferId;
                framebufferId = 0;
            }

            if (fbo != 0)
                manager.EnqueueFramebufferDeletion(fbo);
        }

        private int CreateRenderTexture()
        {
            var (internalFormat, format, type) = ResolveTextureFormat(Options.PixelFormat);

            GL.GenTextures(1, out int textureId);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, Options.Width, Options.Height, 0, format, type, IntPtr.Zero);

            // 上面的绑定绕过了纹理单元绑定缓存，必须重置，避免屏幕路径复用失效的绑定。
            OpenGLTextureBindingCache.Reset();

            return textureId;
        }

        private static (PixelInternalFormat internalFormat, OpenTK.Graphics.OpenGL.PixelFormat format, PixelType type) ResolveTextureFormat(OffscreenPixelFormat pixelFormat)
        {
            return pixelFormat switch
            {
                OffscreenPixelFormat.PlatformDefault or OffscreenPixelFormat.Rgba8888
                    => (PixelInternalFormat.Rgba8, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte),
                OffscreenPixelFormat.Rgba1010102
                    => (PixelInternalFormat.Rgb10A2, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedInt1010102),
                OffscreenPixelFormat.RgbaF16
                    => (PixelInternalFormat.Rgba16f, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.HalfFloat),
                OffscreenPixelFormat.RgbaF32
                    => (PixelInternalFormat.Rgba32f, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.Float),
                OffscreenPixelFormat.Bgra8888
                    => throw new NotSupportedException("OpenGL 后端不支持 Bgra8888 离屏目标（桌面 core GL 无标准 BGRA8 内部格式）。"),
                OffscreenPixelFormat.Gray8
                    => throw new NotSupportedException("OpenGL 后端不支持 Gray8 离屏目标（R8 采样只出红通道，与灰度语义不一致）。"),
                OffscreenPixelFormat.Alpha8
                    => throw new NotSupportedException("OpenGL 后端不支持 Alpha8 离屏目标。"),
                _ => throw new NotSupportedException($"OpenGL 后端不支持的离屏像素格式: {pixelFormat}。"),
            };
        }
    }
}
