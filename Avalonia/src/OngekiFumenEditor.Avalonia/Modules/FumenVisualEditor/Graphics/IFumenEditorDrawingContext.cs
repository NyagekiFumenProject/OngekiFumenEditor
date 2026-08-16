using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics
{
    public interface IFumenEditorDrawingContext : IDrawingContext
    {
        TimeSpan CurrentPlayTime { get; }
        FumenVisualEditorViewModel Editor { get; }

        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);

        bool CheckDrawingVisible(DrawingVisible visible);

        bool CheckVisible(TGrid tGrid);
        bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid);

        double ConvertToY_DefaultSoflanGroup(TGrid tGrid) => ConvertToY(tGrid.TotalUnit, Editor.EditorContext.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY_DefaultSoflanGroup(double tGridUnit) => ConvertToY(tGridUnit, Editor.EditorContext.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY(TGrid tGrid, SoflanList soflans) => ConvertToY(tGrid.TotalUnit, soflans);
        double ConvertToY(double tGridUnit, SoflanList soflans);
    }
}


