using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl;
using OngekiFumenEditor.Modules.FumenPreviewer.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenPreviewer.ViewModels
{
    [Export(typeof(IFumenPreviewer))]
    public class FumenPreviewerViewModel : Tool, IFumenPreviewer, ISchedulable
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private FumenVisualEditorViewModel editor = default;

        public DrawTimeSignatureHelper timeSignatureHelper;
        public DrawStringHelper stringHelper;

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
            }
        }

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

        private static Dictionary<string, IDrawingTarget> drawTargets = new();

        private Dictionary<IDrawingTarget, IEnumerable<OngekiTimelineObjectBase>> drawMap = new();

        public FumenPreviewerViewModel()
        {
            DisplayName = "谱面预览";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
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
        }

        private void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"{id}\t:\t{str}");
        }

        private void RecalcViewProjectionMatrix()
        {
            var projection = Matrix4.Identity * Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
            var view = Matrix4.Identity * Matrix4.CreateTranslation(new Vector3(0, -CurrentPlayTime, 0));

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
                .ToDictionary(x => x.supportId, x => x.target);

            stringHelper = new DrawStringHelper();
            timeSignatureHelper = new DrawTimeSignatureHelper();

            openGLView.Render += (ts) => OnRender(openGLView, ts);
        }

        public IDrawingTarget GetDrawingTarget(string name) => drawTargets.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;

        public void OnRender(GLWpfControl openGLView, TimeSpan ts)
        {
#if DEBUG
            var error = GL.GetError();
            if (error != OpenTK.Graphics.OpenGL.ErrorCode.NoError)
                Log.LogDebug($"OpenGL ERROR!! : {error}");
#endif
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            lock (hits)
            {
                hits.Clear();

                var fumen = Editor?.Fumen;
                if (fumen is null)
                    return;

                if (IsFollowCurrentEditorTime)
                    CurrentPlayTime = (float)Editor.ScrollViewerVerticalOffset;

                var minTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime, fumen.BpmList, 1.0, 240);
                var maxTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime + ViewHeight, fumen.BpmList, 1.0, 240);

                timeSignatureHelper.BeginDraw(this);
                timeSignatureHelper.Draw(fumen);
                timeSignatureHelper.EndDraw();

                drawCall = 0;

                foreach (var objGroup in fumen.GetAllDisplayableObjects(minTGrid, maxTGrid).OfType<OngekiTimelineObjectBase>().GroupBy(x => x.IDShortName))
                {
                    if (GetDrawingTarget(objGroup.Key) is not IDrawingTarget drawingTarget)
                        continue;

                    if (!drawMap.TryGetValue(drawingTarget, out var enums))
                        drawMap[drawingTarget] = objGroup;
                    else
                        drawMap[drawingTarget] = enums.Concat(objGroup);
                }
#if DEBUG
                stopwatch.Restart();
                var prevTime = 0L;
#endif
                foreach (var renderPair in drawMap)
                {
                    var drawingTarget = renderPair.Key;
                    var drawingObjs = renderPair.Value;

                    if (drawingTarget is not null)
                    {
                        drawingTarget.BeginDraw(this);
                        foreach (var obj in drawingObjs.OrderBy(x => x.TGrid))
                        {
                            drawingTarget.Draw(obj, fumen);
                            drawCall++;
                        }
                        drawingTarget.EndDraw();
                    }

#if DEBUG
                    var time = stopwatch.ElapsedTicks;
                    var costTicks = time - prevTime;
                    if (costTicks > castTime.Tick)
                        castTime = (drawingTarget.GetType().Name, costTicks);
                    prevTime = time;
#endif
                }

                drawMap.Clear();

                timeSignatureHelper.BeginDraw(this);
                timeSignatureHelper.DrawTimeSigntureText(stringHelper);
                timeSignatureHelper.EndDraw();

                //Log.LogDebug($"drawcall : {drawCall}");
                SmoothFPSArray[SmoothFPSUpdateIdx] = 1 / (float)ts.TotalSeconds;
                SmoothFPSUpdateIdx++;
                if (SmoothFPSUpdateIdx == SmoothFPSArray.Length)
                    SmoothFPSUpdateIdx = 0;
#if DEBUG
                SmoothFPS2Array[SmoothFPS2UpdateIdx] = 1.0f / (stopwatch.ElapsedMilliseconds / 1000.0f);
                SmoothFPS2UpdateIdx++;
                if (SmoothFPS2UpdateIdx == SmoothFPS2Array.Length)
                    SmoothFPS2UpdateIdx = 0;
#endif
            }
        }

        #region Performence Statictis

        private int SmoothFPSUpdateIdx = 0;
        private int SmoothFPS2UpdateIdx = 0;
        private int drawCall = 0;
        private Stopwatch stopwatch = new Stopwatch();
        private float[] SmoothFPSArray = new float[10];
        private float[] SmoothFPS2Array = new float[10];
        private (string Name, long Tick) castTime = new();
        private StringBuilder stringBuilder = new StringBuilder();

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            stringBuilder.Clear();
            stringBuilder.AppendLine($"FPS:{(int)SmoothFPSArray.Average(),3}/{(int)SmoothFPS2Array.Average(),-3}");
            stringBuilder.AppendLine($"DC:{drawCall}");
            stringBuilder.AppendLine($"TOP:{castTime.Name} ({(new TimeSpan(castTime.Tick).TotalMilliseconds):F2})ms ({castTime.Tick:F2}ticks)");

            DisplayFPS = stringBuilder.ToString();
            castTime = default;
            return Task.CompletedTask;
        }

        #endregion

        #region Selectable Objects Register

        private Dictionary<OngekiObjectBase, Rect> hits = new();

        public void RegisterSelectableObject(OngekiObjectBase obj, Rect rect)
        {
            //rect.Y = rect.Y - CurrentPlayTime;
            hits[obj] = rect;
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

            lock (hits)
            {
                hitResult = hits.AsParallel().Where(x => x.Value.Contains(hitPoint)).ToArray();
                Log.LogDebug($"hit result = {hitResult.FirstOrDefault().Key}");
            }
        }

        #endregion
    }
}
