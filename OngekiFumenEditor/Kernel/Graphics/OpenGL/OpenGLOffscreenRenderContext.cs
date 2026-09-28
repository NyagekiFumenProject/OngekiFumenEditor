using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OpenTK.Graphics.OpenGL;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    /// <summary>
    /// OpenGL offscreen render context: one FBO plus a colour texture created per render (handed to the caller with zero copies).
    /// Renders are drained by <see cref="DefaultOpenGLRenderManagerImpl.PumpOffscreenRenders"/> inside a GL control's render callback
    /// (with the context current), so an offscreen GL control in <c>StartRendering</c> that is visible must exist;
    /// while the control is invisible, requests stay pending until it becomes visible again or Dispose/Term ends them.
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
                    throw new InvalidOperationException("The command list is being used by another render flow, or is already being released.");

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

            // Unlike Skia: OpenGL progress depends on UI ticks, which cannot be assumed to keep coming, so queued requests that
            // have not started are cancelled; a request already being drained still completes and hands its image to the caller.
            queue.CancelQueued(this, new ObjectDisposedException(nameof(OpenGLOffscreenRenderContext), "The offscreen render context has been disposed; render requests that had not started were cancelled."));
        }

        /// <summary>Runs one render inside a drain (with the GL context current).</summary>
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
                    throw new NotSupportedException($"The offscreen framebuffer is incomplete ({status}): pixel format {Options.PixelFormat} may not be supported by the current driver.");

                // flipY: align the GL texture row order (t = 0 at the bottom) with the Bitmap/Skia image row order, so that pasted-back results are not upside down.
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

            // The binding above bypasses the texture unit binding cache and must be reset, otherwise the on-screen path would reuse a stale binding.
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
                    => throw new NotSupportedException("The OpenGL backend does not support the Bgra8888 offscreen target (desktop core GL has no standard BGRA8 internal format)."),
                OffscreenPixelFormat.Gray8
                    => throw new NotSupportedException("The OpenGL backend does not support the Gray8 offscreen target (R8 sampling only returns the red channel, which does not match grayscale semantics)."),
                OffscreenPixelFormat.Alpha8
                    => throw new NotSupportedException("The OpenGL backend does not support the Alpha8 offscreen target."),
                _ => throw new NotSupportedException($"Unsupported offscreen pixel format for the OpenGL backend: {pixelFormat}."),
            };
        }
    }
}
