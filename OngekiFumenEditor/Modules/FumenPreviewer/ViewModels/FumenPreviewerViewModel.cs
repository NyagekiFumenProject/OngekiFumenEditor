using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Editors;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenPreviewer.ViewModels
{
    [Export(typeof(IFumenPreviewer))]
    public class FumenPreviewerViewModel : Tool, IFumenPreviewer, ISchedulable
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private FumenVisualEditorViewModel editor = default;

        public DrawTimeSignatureHelper timeSignatureHelper;

        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                Set(ref editor, value);
                if (IsFollowCurrentEditorTime)
                    CurrentPlayTime = (float)(Editor?.ScrollViewerVerticalOffset ?? 0f);
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        private IPerfomenceMonitor performenceMonitor;
        private float viewWidth = 0;
        public float ViewWidth
        {
            get => viewWidth;
            set
            {
                Set(ref viewWidth, value);
                RecalcViewProjectionMatrix();
            }
        }

        private float viewHeight = 0;
        public float ViewHeight
        {
            get => viewHeight;
            set
            {
                Set(ref viewHeight, value);
                RecalcViewProjectionMatrix();
            }
        }

        private float currentPlayTime;
        public float CurrentPlayTime
        {
            get => currentPlayTime;
            set
            {
                //limit 
                value = Math.Max(value, 0);
                Set(ref currentPlayTime, value);
                RecalcViewProjectionMatrix();
            }
        }

        private bool isFollowCurrentEditorTime = false;
        public bool IsFollowCurrentEditorTime
        {
            get => isFollowCurrentEditorTime;
            set
            {
                Set(ref isFollowCurrentEditorTime, value);
                if (value)
                    CurrentPlayTime = (float)Editor.ScrollViewerVerticalOffset;
            }
        }

        private bool isPreviewPlaying = false;
        public bool IsPreviewPlaying
        {
            get => isPreviewPlaying;
            set
            {
                Set(ref isPreviewPlaying, value);
            }
        }

        private bool isDisplayFPS = false;
        public bool IsDisplayFPS
        {
            get => isDisplayFPS;
            set
            {
                Set(ref isDisplayFPS, value);
            }
        }

        private string displayFPS = "";
        public string DisplayFPS
        {
            get => displayFPS;
            set
            {
                Set(ref displayFPS, value);
            }
        }

        public Matrix4 ViewProjectionMatrix { get; private set; }

        public string SchedulerName => "Fumen Previewer Performance Statictis";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromSeconds(1);

        public OngekiFumen Fumen => Editor.Fumen;

        private static Dictionary<string, IDrawingTarget[]> drawTargets = new();

        private Dictionary<IDrawingTarget, IEnumerable<OngekiTimelineObjectBase>> drawMap = new();

        private IPolygonDrawing polygonDrawing;

        public FumenPreviewerViewModel()
        {
            DisplayName = "谱面预览";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(e.PropertyName is nameof(FumenVisualEditorViewModel.EditorProjectData) or nameof(FumenVisualEditorViewModel.Fumen)))
                return;
            Editor = Editor;
        }

        private void InitOpenGL()
        {
            //GL.Enable(EnableCap.DebugOutput);
            //GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            IoC.Get<ISchedulerManager>().AddScheduler(this);
            Log.LogInfo($"Init OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");
        }

        private void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"{id}\t:\t{str}");
            if (str.Contains("error generated"))
                throw new Exception(str);
        }

        private void RecalcViewProjectionMatrix()
        {
            var projection = Matrix4.Identity * Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
            var view = Matrix4.Identity * Matrix4.CreateTranslation(new Vector3(0, -CurrentPlayTime, 0)) * Matrix4.CreateTranslation(new Vector3(-ViewWidth / 2, -ViewHeight / 2, 0));

            ViewProjectionMatrix = view * projection;
        }

        void IFumenPreviewer.OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
        {
            Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");

            ViewWidth = (float)sizeArg.NewSize.Width;
            ViewHeight = (float)sizeArg.NewSize.Height;
        }

        void IFumenPreviewer.PrepareOpenGLView(GLWpfControl openGLView)
        {
            Log.LogDebug($"ready.");

            InitOpenGL();

            ViewWidth = (float)openGLView.ActualWidth;
            ViewHeight = (float)openGLView.ActualHeight;

            GL.ClearColor(16 / 255.0f, 16 / 255.0f, 16 / 255.0f, 1);
            GL.Viewport(0, 0, (int)ViewWidth, (int)ViewHeight);

            drawTargets = IoC.GetAll<IDrawingTarget>()
                .SelectMany(target => target.DrawTargetID.Select(supportId => (supportId, target)))
                .GroupBy(x => x.supportId).ToDictionary(x => x.Key, x => x.Select(x => x.target).ToArray());

            timeSignatureHelper = new DrawTimeSignatureHelper();

            openGLView.Render += (ts) => OnRender(openGLView, ts);

            polygonDrawing = IoC.Get<IPolygonDrawing>();
        }

        public IDrawingTarget[] GetDrawingTarget(string name) => drawTargets.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;

        public void OnRender(GLWpfControl openGLView, TimeSpan ts)
        {
            performenceMonitor.PostUIRenderTime(ts);
            performenceMonitor.OnBeforeRender();
#if DEBUG
            var error = GL.GetError();
            if (error != OpenTK.Graphics.OpenGL.ErrorCode.NoError)
                Log.LogDebug($"OpenGL ERROR!! : {error}");
#endif
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            hits.Clear();

            var fumen = Editor?.Fumen;
            if (fumen is null)
                return;

            if (IsFollowCurrentEditorTime)
                CurrentPlayTime = (float)Editor.ScrollViewerVerticalOffset;

            var minTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime, fumen.BpmList, 1.0, 240);
            var maxTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime + ViewHeight, fumen.BpmList, 1.0, 240);

            timeSignatureHelper.DrawLines(this);

            foreach (var objGroup in fumen.GetAllDisplayableObjects(minTGrid, maxTGrid).OfType<OngekiTimelineObjectBase>().GroupBy(x => x.IDShortName))
            {
                if (GetDrawingTarget(objGroup.Key) is not IDrawingTarget[] drawingTargets)
                    continue;

                foreach (var drawingTarget in drawingTargets)
                {
                    if (!drawMap.TryGetValue(drawingTarget, out var enums))
                        drawMap[drawingTarget] = objGroup;
                    else
                        drawMap[drawingTarget] = enums.Concat(objGroup);
                }
            }

            foreach (var renderPair in drawMap)
            {
                var drawingTarget = renderPair.Key;
                var drawingObjs = renderPair.Value;

                if (drawingTarget is not null)
                {
                    drawingTarget.Begin(this);
                    foreach (var obj in drawingObjs.OrderBy(x => x.TGrid))
                        drawingTarget.Post(obj);
                    drawingTarget.End();
                }
            }

            drawMap.Clear();

            timeSignatureHelper.DrawTimeSigntureText(this);

            performenceMonitor.OnAfterRender();
        }

        #region Performence Statictis

        private StringBuilder stringBuilder = new StringBuilder();

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            stringBuilder.Clear();

            performenceMonitor.FormatStatistics(stringBuilder);

            DisplayFPS = stringBuilder.ToString();

            performenceMonitor.Clear();

            return Task.CompletedTask;
        }

        #endregion

        #region Selectable Objects Register

        private Dictionary<OngekiObjectBase, Rect> hits = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterSelectableObject(OngekiObjectBase obj, System.Numerics.Vector2 centerPos, System.Numerics.Vector2 size)
        {
            //rect.Y = rect.Y - CurrentPlayTime;
            hits[obj] = new Rect(centerPos.X - size.X / 2, centerPos.Y - size.Y / 2, size.X, size.Y);
        }

        #endregion

        #region UserActions

        public void OnMouseWheel(MouseWheelEventArgs args)
        {
            CurrentPlayTime += (args.Delta > 0 ? 2 : -2) * (Keyboard.IsKeyDown(Key.LeftShift) ? 10 : 1);
            Log.LogDebug($"CurrentPlayTime = {CurrentPlayTime}");
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseEventArgs;
            var hitPoint = arg.GetPosition(e.Source);
            hitPoint.Y = (e.Source.ActualHeight - hitPoint.Y) + CurrentPlayTime;
            var hitResult = Enumerable.Empty<KeyValuePair<OngekiObjectBase, Rect>>();

            hitResult = hits.AsParallel().Where(x => x.Value.Contains(hitPoint)).ToArray();
            Log.LogDebug($"hit result = {hitResult.FirstOrDefault().Key}");
        }

        #endregion
    }
}
