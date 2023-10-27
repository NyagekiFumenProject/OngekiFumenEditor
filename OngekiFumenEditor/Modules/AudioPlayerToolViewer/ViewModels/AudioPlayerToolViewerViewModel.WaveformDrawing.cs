using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static OngekiFumenEditor.Kernel.Graphics.IDrawingContext;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
	public partial class AudioPlayerToolViewerViewModel : IWaveformDrawingContext
	{
		private float viewWidth;
		private float viewHeight;
		private int renderViewWidth;
		private int renderViewHeight;
		private IPerfomenceMonitor performenceMonitor;
		private ISamplePeak samplePeak;
		private CancellationTokenSource loadWaveformTask;
		private CancellationTokenSource resampleTaskCancelTokenSource;
		private TaskCompletionSource initTask = new TaskCompletionSource();

		private PeakPointCollection rawPeakData;
		private PeakPointCollection usingPeakData;

		public VisibleRect Rect { get; private set; }

		public Matrix4 ProjectionMatrix { get; private set; }
		public Matrix4 ViewMatrix { get; private set; }
		public Matrix4 ViewProjectionMatrix { get; private set; }

		public IPerfomenceMonitor PerfomenceMonitor => performenceMonitor;

		public TimeSpan CurrentTime { get; private set; }
		public TimeSpan AudioTotalDuration => AudioPlayer?.Duration ?? default;

		private IWaveformDrawing waveformDrawing;
		public IWaveformDrawing WaveformDrawing
		{
			get => waveformDrawing;
			set
			{
				Set(ref waveformDrawing, value);
			}
		}

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

		private bool isShowWaveform = true;
		public bool IsShowWaveform
		{
			get => isShowWaveform && Properties.AudioPlayerToolViewerSetting.Default.EnableWaveformDisplay;
			set => Set(ref isShowWaveform, value);
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

		public void OnRenderSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
		{
			Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");
			var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
			var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

			viewWidth = (float)sizeArg.NewSize.Width;
			viewHeight = (float)sizeArg.NewSize.Height;
			renderViewWidth = (int)(sizeArg.NewSize.Width * dpiX);
			renderViewHeight = (int)(sizeArg.NewSize.Height * dpiY);

			RecalcViewProjectionMatrix();
		}

		public async void PrepareRender(GLWpfControl glView)
		{
			Log.LogDebug($"ready.");
			await IoC.Get<IDrawingManager>().CheckOrInitGraphics();

			InitRender();
			var dpiX = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
			var dpiY = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;

			viewWidth = (float)glView.ActualWidth;
			viewHeight = (float)glView.ActualHeight;
			renderViewWidth = (int)(glView.ActualWidth * dpiX);
			renderViewHeight = (int)(glView.ActualHeight * dpiY);

			RecalcViewProjectionMatrix();

			//暂时没有需要显示检测的必要?
			//performenceMonitor = IoC.Get<IPerfomenceMonitor>();
			performenceMonitor = new DummyPerformenceMonitor();

			glView.Render += Render;
		}

		private void InitRender()
		{
			samplePeak = IoC.Get<ISamplePeak>();
			WaveformDrawing = IoC.Get<IWaveformDrawing>();
			initTask.SetResult();
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
			await initTask.Task;
			if (cancelToken.IsCancellationRequested || player is null || samplePeak is null)
				return;
			var sampleData = await player.GetSamplesAsync();
			rawPeakData = sampleData is not null ? samplePeak.GetPeakValues(sampleData) : null;
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
				var newPeakData = rawPeakData is null ? default : await rawPeakData?.GenerateSimplfiedAsync(ResampleSize, tokenSource.Token);
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

		public void Render(TimeSpan ts)
		{
#if DEBUG
			GLUtility.CheckError();
#endif

			GL.ClearColor(16 / 255f, 16 / 255f, 16 / 255f, 1f);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			if (Editor is null || !IsShowWaveform)
				return;

			PerfomenceMonitor.PostUIRenderTime(ts);
			PerfomenceMonitor.OnBeforeRender();

			GL.Viewport(0, 0, renderViewWidth, renderViewHeight);

			UpdateDrawingContext();

			if (usingPeakData is not null)
				WaveformDrawing.Draw(this, usingPeakData);

			PerfomenceMonitor.OnAfterRender();
		}

		private void UpdateDrawingContext()
		{
			Rect = new VisibleRect(new(0 + viewWidth, 0), new(0, 0 + viewHeight));

			if (AudioPlayer?.IsPlaying ?? false)
				CurrentTime = AudioPlayer.CurrentTime;
			else
			{
				var tGrid = Editor?.GetCurrentTGrid();
				if (tGrid is null)
					return;
				var editorAudioTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, Editor);
				CurrentTime = editorAudioTime;
			}
		}
	}
}
