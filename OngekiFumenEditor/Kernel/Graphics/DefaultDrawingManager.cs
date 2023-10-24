//#define OGL_LOG
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Graphics
{
    [Export(typeof(IDrawingManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultDrawingManager : IDrawingManager
    {
        TaskCompletionSource initTaskSource = new TaskCompletionSource();
        bool startedInit = false;

        public Task CheckOrInitGraphics()
        {
            if (!startedInit)
            {
                startedInit = true;
                Dispatcher.CurrentDispatcher.InvokeAsync(OnInitOpenGL);
            }

            return initTaskSource.Task;
        }

        private void OnInitOpenGL()
        {

#if DEBUG && OGL_LOG
            GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);
            GL.Enable(EnableCap.DebugOutput);
#endif

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Log.LogInfo($"Prepare OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");

            initTaskSource.SetResult();
        }

        private static void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            if (id == 131185)
                return;

            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"[{source}.{type}]{id}:  {str}");
            if (str.Contains("error"))
                throw new Exception(str);
        }

        public Task WaitForGraphicsInitializationDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }

        public Task CreateContext(GLWpfControl glView, CancellationToken cancellation = default)
        {
            var flag = ContextFlags.Default;
#if DEBUG && OGL_LOG
            flag = flag | ContextFlags.Debug;
#endif
            var profile = ContextProfile.Core;

            Log.LogDebug($"flag = {flag}, profile = {profile}");
            glView.Start(new()
            {
                MajorVersion = 3,
                MinorVersion = 3,
                GraphicsContextFlags = flag,
                GraphicsProfile = profile
            });

            return Task.CompletedTask;
        }
    }
}
