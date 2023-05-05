using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
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
            //GL.Enable(EnableCap.DebugOutput);
            //GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Log.LogInfo($"Init OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");

            initTaskSource.SetResult();
        }

        private void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"{id}\t:\t{str}");
            if (str.Contains("error generated"))
                throw new Exception(str);
        }

        public Task WaitForGraphicsInitializationDone(CancellationToken cancellation)
        {
            return initTaskSource.Task;
        }
    }
}
