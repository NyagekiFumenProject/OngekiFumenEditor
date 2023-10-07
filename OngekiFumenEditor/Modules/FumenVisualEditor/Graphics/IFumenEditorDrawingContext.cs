using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Windows;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics
{
    public interface IFumenEditorDrawingContext : IDrawingContext
    {
        float CurrentPlayTime { get; }
        FumenVisualEditorViewModel Editor { get; }
        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);

        bool CheckDrawingVisible(DrawingVisible visible);
        bool CheckVisible(TGrid tGrid);

        double ConvertToY(TGrid tGrid) => ConvertToY(tGrid.TotalUnit);
        double ConvertToY(double tGridUnit);
    }
}
