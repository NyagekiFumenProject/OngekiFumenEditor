using Gemini.Framework;
using OngekiFumenEditor.Base;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System.Windows;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public interface IFumenEditorDrawingContext
    {
        float ViewWidth { get; }
        float ViewHeight { get; }
        float CurrentPlayTime { get; }
        Matrix4 ViewProjectionMatrix { get; }
        OngekiFumen Fumen { get; }

        void PrepareOpenGLView(GLWpfControl glView);
        void OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs e);
        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);
    }
}
