using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenPreviewer
{
    public interface IFumenPreviewer : ITool
    {
        float ViewWidth { get; }
        float ViewHeight { get; }
        float CurrentPlayTime { get; set; }
        Matrix4 ViewProjectionMatrix { get; }

        void PrepareOpenGLView(GLWpfControl glView);
        void OnOpenGLViewSizeChanged(GLWpfControl glView, SizeChangedEventArgs e);
        void RegisterSelectableObject(OngekiObjectBase obj, Rect rect);
    }
}
