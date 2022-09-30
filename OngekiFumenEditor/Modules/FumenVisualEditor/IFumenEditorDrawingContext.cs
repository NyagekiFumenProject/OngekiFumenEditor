using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Windows;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public interface IFumenEditorDrawingContext
    {
        /// <summary>
        /// 已左手L形坐标系为世界坐标系的可视区域
        /// </summary>
        public struct VisibleRect
        {
            public Vector2 TopLeft;
            public Vector2 ButtomRight;

            public float Width => ButtomRight.X - TopLeft.X;
            public float Height => TopLeft.Y - ButtomRight.Y;

            public float MinY => ButtomRight.Y;
            public float MaxY => TopLeft.Y;

            public float MinX => TopLeft.X;
            public float MaxX => ButtomRight.X;

            public TGrid VisiableMinTGrid;
            public TGrid VisiableMaxTGrid;
        }

        //values are updating by frame
        public VisibleRect Rect { get; }

        public float ViewWidth => Rect.Width;
        public float ViewHeight => Rect.Height;

        float CurrentPlayTime { get; }
        Matrix4 ProjectionMatrix { get; }
        Matrix4 ViewMatrix { get; }
        Matrix4 ViewProjectionMatrix { get; }
        //

        FumenVisualEditorViewModel Editor { get; }

        void PrepareOpenGLView(GLWpfControl glView);
        void OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs e);

        void Render(TimeSpan ts);

        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);
    }
}
