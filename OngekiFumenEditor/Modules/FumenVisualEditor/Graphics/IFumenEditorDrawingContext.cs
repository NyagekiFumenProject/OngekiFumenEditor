using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics
{
    public interface IFumenEditorDrawingContext : IDrawingContext
    {
        TimeSpan CurrentPlayTime { get; }
        FumenVisualEditorViewModel Editor { get; }

        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);

        bool CheckDrawingVisible(DrawingVisible visible);

        bool CheckVisible(TGrid tGrid);
        bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid);

        double ConvertToY_DefaultSoflanGroup(TGrid tGrid) => ConvertToY(tGrid.TotalUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY_DefaultSoflanGroup(double tGridUnit) => ConvertToY(tGridUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY(TGrid tGrid, SoflanList soflans) => ConvertToY(tGrid.TotalUnit, soflans);
        double ConvertToY(double tGridUnit, SoflanList soflans);
    }
}
