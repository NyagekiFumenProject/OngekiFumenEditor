using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl;
using OngekiFumenEditor.Modules.FumenPreviewer.Views;
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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenPreviewer.ViewModels
{
    [Export(typeof(IFumenPreviewer))]
    public class FumenPreviewerViewModel : Tool, IFumenPreviewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private FumenVisualEditorViewModel editor = default;

        public event System.Action OnViewResized;

        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                Set(ref editor, value);
            }
        }

        private float viewWidth = 0;
        public float ViewWidth
        {
            get=> viewWidth;
            set
            {
                Set(ref viewWidth, value);
                ProjectionMatrix = Matrix4.Identity * Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
            }
        }

        private float viewHeight = 0;
        public float ViewHeight
        {
            get => viewHeight;
            set
            {
                Set(ref viewHeight, value);
                ProjectionMatrix = Matrix4.Identity * Matrix4.CreateOrthographic(ViewWidth, ViewHeight, -1, 1);
            }
        }

        public float CurrentPlayTime { get; private set; }

        private Matrix4 viewMatrix = Matrix4.Identity;
        public Matrix4 ViewMatrix
        {
            get => viewMatrix;
            set
            {
                Set(ref viewMatrix, value);
                ViewProjectionMatrix = ProjectionMatrix * ViewMatrix;
            }
        }

        private Matrix4 projectionMatrix = Matrix4.Identity;
        public Matrix4 ProjectionMatrix
        {
            get => projectionMatrix;
            set
            {
                Set(ref projectionMatrix, value);
                ViewProjectionMatrix = ProjectionMatrix * ViewMatrix;
            }
        }

        public Matrix4 ViewProjectionMatrix { get; private set; }

        private static Dictionary<string, IDrawingTarget> drawTargets = new();
        private DummyDrawTarget dummyDraw;

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
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        void IFumenPreviewer.OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs sizeArg)
        {
            Log.LogDebug($"new size: {sizeArg.NewSize} , glView.RenderSize = {glView.RenderSize}");

            ViewWidth = (float)sizeArg.NewSize.Width;
            ViewHeight = (float)sizeArg.NewSize.Height;

            OnViewResized?.Invoke();
        }

        void IFumenPreviewer.PrepareOpenGLView(GLWpfControl openGLView)
        {
            Log.LogDebug($"ready.");

            InitOpenGL();

            ViewWidth = (float)openGLView.ActualWidth;
            ViewHeight = (float)openGLView.ActualHeight;

            GL.ClearColor(System.Drawing.Color.Black);
            GL.Viewport(0, 0, (int)ViewWidth, (int)ViewHeight);

            drawTargets = IoC.GetAll<IDrawingTarget>().ToDictionary(x => x.DrawTargetID, x => x);

            dummyDraw = IoC.Get<DummyDrawTarget>();

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

            dummyDraw.BeginDraw();
            dummyDraw.Draw(null, null);
            dummyDraw.EndDraw();
            /*
            var fumen = Editor?.Fumen;
            if (fumen is null)
                return;

            foreach (var objGroup in fumen.GetAllDisplayableObjects().OfType<OngekiObjectBase>().GroupBy(x => x.IDShortName))
            {
                if (drawTargets.TryGetValue(objGroup.Key, out var drawingTarget))
                {
                    drawingTarget.BeginDraw();
                    foreach (var obj in objGroup)
                        drawingTarget.Draw(obj, fumen);
                    drawingTarget.EndDraw();
                }
            }
            */
        }
    }
}
