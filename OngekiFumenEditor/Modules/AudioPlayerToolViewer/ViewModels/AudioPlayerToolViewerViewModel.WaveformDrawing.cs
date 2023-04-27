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
        private CancellationTokenSource resampleTaskCancelTokenSource;

        private PeakPointCollection rawPeakData;
        private PeakPointCollection usingPeakData;

        public VisibleRect Rect { get; private set; }

        public Matrix4 ProjectionMatrix { get; private set; }
        public Matrix4 ViewMatrix { get; private set; }
        public Matrix4 ViewProjectionMatrix { get; private set; }

        public IPerfomenceMonitor PerfomenceMonitor => performenceMonitor;

        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan AudioTotalDuration => AudioPlayer?.Duration ?? default;

        private int resampleSize = Properties.AudioPlayerToolViewerSetting.Default.ResampleSize;
        public int ResampleSize
        {
            get => resampleSize;
            set
            {
                Set(ref resampleSize, value);
                ResamplePeak();
                Properties.AudioPlayerToolViewerSetting.Default.ResampleSize = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float waveformVecticalScale = Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale;
        public float WaveformVecticalScale
        {
            get => waveformVecticalScale;
            set
            {
                Set(ref waveformVecticalScale, value);
                Properties.AudioPlayerToolViewerSetting.Default.WaveformVecticalScale = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float durationMsPerPixel = Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel;
        public float DurationMsPerPixel
        {
            get => durationMsPerPixel;
            set
            {
                Set(ref durationMsPerPixel, value);
                Properties.AudioPlayerToolViewerSetting.Default.DurationMsPerPixel = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

        private float currentTimeXOffset = Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset;
        public float CurrentTimeXOffset
        {
            get => currentTimeXOffset;
            set
            {
                Set(ref currentTimeXOffset, value);
                Properties.AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset = value;
                Properties.AudioPlayerToolViewerSetting.Default.Save();
            }
        }

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
            rawPeakData = samplePeak.GetPeakValues(sampleData);
            ResamplePeak();
        }

        private async void ResamplePeak()
        {
            resampleTaskCancelTokenSource?.Cancel();
            var tokenSource = new CancellationTokenSource();
            resampleTaskCancelTokenSource = tokenSource;

            if (ResampleSize == 0)
                usingPeakData = rawPeakData;
            else
            {
                var newPeakData = await rawPeakData.GenerateSimplfiedAsync(ResampleSize, tokenSource.Token);
                if (!tokenSource.IsCancellationRequested)
                    usingPeakData = newPeakData;
            }
        }

        private void CleanWaveform()
        {
            loadWaveformTask?.Cancel();
            loadWaveformTask = null;
            rawPeakData = null;
            usingPeakData = null;
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

            GL.ClearColor(16 / 255f, 16 / 255f, 16 / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, (int)viewWidth, (int)viewHeight);

            UpdateDrawingContext();

            if (usingPeakData is not null)
                waveformDrawing.Draw(this, usingPeakData);

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
    }
}
