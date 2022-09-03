using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl;
using OngekiFumenEditor.Modules.FumenPreviewer.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenPreviewer.ViewModels
{
    [Export(typeof(IFumenPreviewer))]
    public class FumenPreviewerViewModel : Tool, IFumenPreviewer
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


        public Matrix4 ViewProjectionMatrix { get; private set; }

        private static Dictionary<string, IDrawingTarget> drawTargets = new();

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

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Viewport(0, 0, (int)ViewWidth, (int)ViewHeight);

            drawTargets = IoC.GetAll<IDrawingTarget>()
                .SelectMany(target => target.DrawTargetID.Select(supportId => (supportId, target)))
                .ToDictionary(x => x.supportId, x => x.target);

            timeSignatureHelper = new DrawTimeSignatureHelper();

            openGLView.Render += (ts) => OnRender(openGLView, ts);
        }

        public void OnRender(GLWpfControl openGLView, TimeSpan ts)
        {
#if DEBUG
            var error = GL.GetError();
            if (error != OpenTK.Graphics.OpenGL.ErrorCode.NoError)
                Log.LogDebug($"OpenGL ERROR!! : {error}");
#endif
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var fumen = Editor?.Fumen;
            if (fumen is null)
                return;

            if (IsFollowCurrentEditorTime)
                CurrentPlayTime = (float)Editor.ScrollViewerVerticalOffset;

            var minTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime, fumen.BpmList, 240);
            var maxTGrid = TGridCalculator.ConvertYToTGrid(CurrentPlayTime + ViewHeight, fumen.BpmList, 240);

            timeSignatureHelper.Draw(fumen);
            var drawCall = 0;

            foreach (var objGroup in fumen.GetAllDisplayableObjects(minTGrid, maxTGrid).OfType<OngekiObjectBase>().GroupBy(x => x.IDShortName))
            {
                if (drawTargets.TryGetValue(objGroup.Key, out var drawingTarget))
                {
                    drawingTarget.BeginDraw();
                    foreach (var obj in objGroup)
                    {
                        drawingTarget.Draw(obj, fumen);
                        drawCall++;
                    }
                    drawingTarget.EndDraw();
                }
            }

            //Log.LogDebug($"drawcall : {drawCall}");
        }

        #region UserActions

        public void OnMouseWheel(MouseWheelEventArgs args)
        {
            CurrentPlayTime += (args.Delta > 0 ? 2 : -2) * (Keyboard.IsKeyDown(Key.LeftShift) ? 10 : 1);
            Log.LogDebug($"CurrentPlayTime = {CurrentPlayTime}");
        }

        #endregion
    }
}
