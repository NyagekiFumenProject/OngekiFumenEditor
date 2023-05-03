using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using OpenTK.Graphics.OpenGL;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Kernel.Graphics;
using static OngekiFumenEditor.Kernel.Graphics.IDrawingContext;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.IFumenEditorDrawingContext;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using static System.Windows.Forms.AxHost;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument, ISchedulable, IFumenEditorDrawingContext
    {
        private IPerfomenceMonitor performenceMonitor;

        private DrawTimeSignatureHelper timeSignatureHelper;
        private DrawXGridHelper xGridHelper;
        private DrawJudgeLineHelper judgeLineHelper;
        private DrawSelectingRangeHelper selectingRangeHelper;
        private DrawSelectableObjectTextureHelper selectableObjectHelper;

        private StringBuilder stringBuilder = new StringBuilder();

        private List<CacheDrawXLineResult> cachedMagneticXGridLines = new();
        public IEnumerable<CacheDrawXLineResult> CachedMagneticXGridLines => cachedMagneticXGridLines;

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

        public float CurrentPlayTime => (float)(ScrollViewerVerticalOffset);

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
                displayFPS = value;
                NotifyOfPropertyChange(() => DisplayFPS);
            }
        }

        public Matrix4 ViewMatrix { get; private set; }
        public Matrix4 ProjectionMatrix { get; private set; }
        public Matrix4 ViewProjectionMatrix { get; private set; }

        public string SchedulerName => "Fumen Previewer Performance Statictis";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromSeconds(1);

        public FumenVisualEditorViewModel Editor => this;

        public VisibleRect Rect { get; set; } = default;
        public VisibleTGridRange TGridRange { get; set; } = default;

        public IPerfomenceMonitor PerfomenceMonitor => performenceMonitor;

        private static Dictionary<string, IDrawingTarget[]> drawTargets = new();
        private IDrawingTarget[] drawTargetOrder;
        private Dictionary<IDrawingTarget, IEnumerable<OngekiTimelineObjectBase>> drawMap = new();

        private void InitOpenGL()
        {
            //GL.Enable(EnableCap.DebugOutput);
            //GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(OnOpenGLDebugLog, IntPtr.Zero);

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            Log.LogInfo($"Init OpenGL version : {GL.GetInteger(GetPName.MajorVersion)}.{GL.GetInteger(GetPName.MinorVersion)}");
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            InitExtraMenuItems();
        }

        private void RecalcViewProjectionMatrix()
        {
            ProjectionMatrix =
                Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
            ViewMatrix =
                Matrix4.CreateTranslation(new Vector3(-ViewWidth / 2, -CurrentPlayTime - ViewHeight / 2 + (float)Setting.JudgeLineOffsetY, 0));

            ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        }

        public void OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
        {
            Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");

            ViewWidth = (float)sizeArg.NewSize.Width;
            ViewHeight = (float)sizeArg.NewSize.Height;
        }

        public void PrepareOpenGLView(GLWpfControl openGLView)
        {
            Log.LogDebug($"ready.");

            InitOpenGL();

            ViewWidth = (float)openGLView.ActualWidth;
            ViewHeight = (float)openGLView.ActualHeight;

            drawTargets = IoC.GetAll<IDrawingTarget>()
                .SelectMany(target => target.DrawTargetID.Select(supportId => (supportId, target)))
                .GroupBy(x => x.supportId).ToDictionary(x => x.Key, x => x.Select(x => x.target).ToArray());

            drawTargetOrder = drawTargets.Values.SelectMany(x => x).OrderBy(x => x.DefaultRenderOrder).Distinct().ToArray();

            timeSignatureHelper = new DrawTimeSignatureHelper();
            xGridHelper = new DrawXGridHelper();
            judgeLineHelper = new DrawJudgeLineHelper();
            selectingRangeHelper = new DrawSelectingRangeHelper();
            selectableObjectHelper = new DrawSelectableObjectTextureHelper();

            performenceMonitor = IoC.Get<IPerfomenceMonitor>();

            openGLView.Render += (ts) => OnRender(openGLView, ts);
        }

        public IDrawingTarget[] GetDrawingTarget(string name) => drawTargets.TryGetValue(name, out var drawingTarget) ? drawingTarget : default;

        List<IDisplayableObject> obj = new List<IDisplayableObject>();

        private IEnumerable<IDisplayableObject> GetDisplayableObjects(OngekiFumen fumen, TGrid min, TGrid max)
        {
            var first = Enumerable.Empty<IDisplayableObject>()
                   //.Concat(fumen.Bells.BinaryFindRange(min, max))
                   .Concat(fumen.Flicks.BinaryFindRange(min, max))
                   .Concat(fumen.MeterChanges.Skip(1)) //not show first meter
                   .Concat(fumen.BpmList.Skip(1)) //not show first bpm
                   .Concat(fumen.ClickSEs.BinaryFindRange(min, max))
                   .Concat(fumen.LaneBlocks.GetVisibleStartObjects(min, max))
                   .Concat(fumen.Soflans)
                   .Concat(fumen.EnemySets.BinaryFindRange(min, max))
                   //.Concat(fumen.Bullets.BinaryFindRange(min, max))
                   .Concat(fumen.Lanes.GetVisibleStartObjects(min, max))
                   .Concat(fumen.Taps.BinaryFindRange(min, max))
                   .Concat(fumen.Holds.GetVisibleStartObjects(min, max))
                   .Concat(fumen.SvgPrefabs)
                   .Concat(fumen.Beams)
                   .Distinct();

            /*
             这里考虑到有spd<1的子弹/Bell会提前出现的情况，因此得分状态分别去选择
             */
            obj.Clear();
            if (Editor.EditorObjectVisibility != Visibility.Visible)
            {
                //todo 还能再次优化
                bool check(IBulletPalleteReferencable bell)
                {
                    var appearOffsetTime = ViewHeight / (bell.ReferenceBulletPallete?.Speed ?? 1f);

                    var toTime = TGridCalculator.ConvertTGridToY(bell.TGrid, this);
                    var fromTime = toTime - appearOffsetTime;

                    return MathUtils.IsInRange(fromTime, toTime, Rect.MinY, Rect.MaxY);
                }

                foreach (var x in fumen.Bells
                    .AsEnumerable<IBulletPalleteReferencable>()
                    .Concat(fumen.Bullets))
                {
                    if (check(x))
                        obj.Add(x);
                }
            }
            else
            {
                obj.AddRange(fumen.Bells.BinaryFindRange(min, max));
                obj.AddRange(fumen.Bullets.BinaryFindRange(min, max));
            }

            return first.Concat(obj).SelectMany(x => x.GetDisplayableObjects());
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
            GL.ClearColor(16 / 255.0f, 16 / 255.0f, 16 / 255.0f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, (int)ViewWidth, (int)ViewHeight);

            hits.Clear();

            var fumen = Fumen;
            if (fumen is null)
                return;

            var minY = (float)(CurrentPlayTime - Setting.JudgeLineOffsetY);
            var minTGrid = TGridCalculator.ConvertYToTGrid(minY, this) ?? TGrid.Zero;
            var maxTGrid = TGridCalculator.ConvertYToTGrid(minY + ViewHeight, this);

            //todo 这里就要计算可视区域了
            Rect = new VisibleRect(new(ViewWidth, minY), new(0, minY + ViewHeight));
            TGridRange = new VisibleTGridRange(minTGrid, maxTGrid);

            RecalculateMagaticXGridLines();

            timeSignatureHelper.DrawLines(this);
            xGridHelper.DrawLines(this, CachedMagneticXGridLines);

            foreach (var objGroup in GetDisplayableObjects(fumen, minTGrid, maxTGrid).OfType<OngekiTimelineObjectBase>().GroupBy(x => x.IDShortName))
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

            foreach (var drawingTarget in drawTargetOrder)
            {
                if (drawMap.TryGetValue(drawingTarget, out var drawingObjs))
                {
                    drawingTarget.Begin(this);
                    foreach (var obj in drawingObjs.OrderBy(x => x.TGrid))
                        drawingTarget.Post(obj);
                    drawingTarget.End();
                }
            }

            drawMap.Clear();

            timeSignatureHelper.DrawTimeSigntureText(this);
            xGridHelper.DrawXGridText(this, CachedMagneticXGridLines);
            judgeLineHelper.Draw(this);
            selectingRangeHelper.Draw(this);

            performenceMonitor.OnAfterRender();
        }

        private void OnOpenGLDebugLog(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var str = Marshal.PtrToStringAnsi(message, length);
            Log.LogDebug($"{id}\t:\t{str}");
            if (str.Contains("error generated"))
                throw new Exception(str);
        }

        public void Redraw(RedrawTarget target)
        {
            if (target.HasFlag(RedrawTarget.ScrollBar))
                RecalculateScrollBar();
        }

        public void OnLoaded(ActionExecutionContext e)
        {
            var scrollViewer = e.Source as FrameworkElement;
            var view = e.View as FrameworkElement;
            Redraw(RedrawTarget.All);
        }

        private void RecalculateMagaticXGridLines()
        {
            cachedMagneticXGridLines.Clear();

            var width = ViewWidth;
            var xUnitSpace = (float)Editor.Setting.XGridUnitSpace;
            var maxDisplayXUnit = Editor.Setting.XGridDisplayMaxUnit;

            var unitSize = (float)XGridCalculator.CalculateXUnitSize(maxDisplayXUnit, width, xUnitSpace);
            var totalUnitValue = 0f;

            for (float totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += xUnitSpace;

                cachedMagneticXGridLines.Add(new()
                {
                    X = totalLength,
                    XGridTotalUnit = totalUnitValue,
                    XGridTotalUnitDisplay = totalUnitValue.ToString()
                });

                cachedMagneticXGridLines.Add(new()
                {
                    X = (width / 2) - (totalLength - (width / 2)),
                    XGridTotalUnit = -totalUnitValue,
                    XGridTotalUnitDisplay = (-totalUnitValue).ToString()
                });
            }
            cachedMagneticXGridLines.Add(new()
            {
                X = width / 2,
                XGridTotalUnit = 0f,
            });
        }

        public void OnSizeChanged(ActionExecutionContext e)
        {
            var scrollViewer = e.Source as AnimatedScrollViewer;
            scrollViewer?.InvalidateMeasure();

            var view = GetView() as FrameworkElement;
            Redraw(RedrawTarget.All);
        }

        public void OnSchedulerTerm()
        {

        }

        public Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (IsDisplayFPS)
            {
                stringBuilder.Clear();

                performenceMonitor?.FormatStatistics(stringBuilder);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"View: {ViewWidth}x{ViewHeight}");

                DisplayFPS = stringBuilder.ToString();

                performenceMonitor?.Clear();

            }
            return Task.CompletedTask;
        }

        public void Render(TimeSpan ts)
        {
            OnRender(default, ts);
        }
    }
}
