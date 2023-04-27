using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static OngekiFumenEditor.Kernel.Graphics.IDrawingContext;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    public partial class AudioPlayerToolViewerViewModel : IWaveformDrawingContext
    {
        private float viewWidth;
        private float viewHeight;
        private IPerfomenceMonitor performenceMonitor;
        private ISamplePeak samplePeak;
        private IWaveformDrawing waveformDrawing;
        private CancellationTokenSource loadWaveformTask;
        private ISamplePeak.PeakPointCollection peakData;

        public VisibleRect Rect { get; private set; }

        public Matrix4 ProjectionMatrix { get; private set; }
        public Matrix4 ViewMatrix { get; private set; }
        public Matrix4 ViewProjectionMatrix { get; private set; }

        public IPerfomenceMonitor PerfomenceMonitor => performenceMonitor;

        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan AudioTotalDuration => AudioPlayer?.Duration ?? default;
        public float DurationMsPerPixel { get; private set; } = 10.0f;
        public float CurrentTimeXOffset { get; private set; } = 30f;

        public FumenVisualEditorViewModel EditorViewModel => Editor;

        private void RecalcViewProjectionMatrix()
        {
            ProjectionMatrix =
                Matrix4.CreateOrthographic(viewWidth, viewHeight, -1, 1);
            ViewMatrix =
                Matrix4.CreateTranslation(new Vector3(0, 0, 0)); //todo

            ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        }

        public void OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
        {
            Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");

            viewWidth = (float)sizeArg.NewSize.Width;
            viewHeight = (float)sizeArg.NewSize.Height;
            RecalcViewProjectionMatrix();
        }

        private void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"{id}\t:\t{str}");
            if (str.Contains("error generated"))
                throw new Exception(str);
        }

        private void InitOpenGL()
        {
            //GL.Enable(EnableCap.DebugOutput);
            //GL.Enable(EnableCap.DebugOutputSynchronous);
            //GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Log.LogInfo($"Init OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");
        }

        public void PrepareOpenGLView(GLWpfControl glView)
        {
            Log.LogDebug($"ready.");

            InitOpenGL();

            InitRender();

            viewWidth = (float)glView.ActualWidth;
            viewHeight = (float)glView.ActualHeight;
            RecalcViewProjectionMatrix();

            performenceMonitor = IoC.Get<IPerfomenceMonitor>();

            glView.Render += (ts) => OnRender(glView, ts);
        }

        private void InitRender()
        {
            samplePeak = IoC.Get<ISamplePeak>();
            waveformDrawing = IoC.Get<IWaveformDrawing>();
        }

        public void Render(TimeSpan ts)
        {
            OnRender(default, ts);
        }

        private void PrepareWaveform(IAudioPlayer player)
        {
            CleanWaveform();
            loadWaveformTask = new CancellationTokenSource();
            var cancelToken = loadWaveformTask.Token;

            Task.Run(() => OnPrepareWaveform(player, cancelToken), cancelToken);
        }

        private async void OnPrepareWaveform(IAudioPlayer player, CancellationToken cancelToken)
        {
            if (cancelToken.IsCancellationRequested || player is null)
                return;
            var sampleData = await player.GetSamplesAsync();
            peakData = samplePeak.GetPeakValues(sampleData);
        }

        private void CleanWaveform()
        {
            loadWaveformTask?.Cancel();
            loadWaveformTask = null;
            peakData = null;
        }

        public void OnRender(GLWpfControl openGLView, TimeSpan ts)
        {
            performenceMonitor.PostUIRenderTime(ts);
            performenceMonitor.OnBeforeRender();

#if DEBUG
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
                Log.LogDebug($"OpenGL ERROR!! : {error}");
#endif

            GL.ClearColor(0 / 255f, 0 / 255f, 0 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, (int)viewWidth, (int)viewHeight);

            UpdateDrawingContext();

            if (peakData is not null)
                waveformDrawing.Draw(this, peakData);

            performenceMonitor.OnAfterRender();
        }

        private void UpdateDrawingContext()
        {
            Rect = new VisibleRect(new(0 + viewWidth, 0), new(0, 0 + viewHeight));

            if (AudioPlayer?.IsPlaying ?? false)
                CurrentTime = AudioPlayer.CurrentTime;
            else
            {
                var tGrid = Editor?.GetCurrentJudgeLineTGrid();
                if (tGrid is null)
                    return;
                var editorAudioTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, Editor);
                CurrentTime = editorAudioTime;
            }
        }

        #region User Interection

        public void OnMouseWheel(ActionExecutionContext ctx)
        {
            var arg = ctx.EventArgs as MouseWheelEventArgs;
            DurationMsPerPixel = (float)Math.Max(2.5f, DurationMsPerPixel + Math.Sign(arg.Delta) * 2.5);
        }

        #endregion
    }
}
