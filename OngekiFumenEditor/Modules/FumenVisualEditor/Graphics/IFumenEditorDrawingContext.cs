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

        /// <summary>
        /// 本帧统一的时间快照；帧外调用时回退到实时播放时间。
        /// 渲染相关代码应使用它，而不是 <see cref="CurrentPlayTime"/>。
        /// </summary>
        TimeSpan FrameTime => CurrentDrawingTargetContext is { } context ? context.CurrentTime : CurrentPlayTime;

        /// <summary>
        /// 本帧统一的当前 TGrid 快照；帧外或未填充时回退到实时值。
        /// 渲染相关代码应使用它，而不是 editor 的实时 <c>GetCurrentTGrid()</c>。
        /// </summary>
        TGrid FrameTGrid => CurrentDrawingTargetContext is { CurrentTGrid: { } tGrid } ? tGrid : Editor.GetCurrentTGrid();

        /// <summary>
        /// 本帧统一的视口 TGrid 快照（预览模式下由滚动位置决定，可能领先于 <see cref="FrameTGrid"/>）；
        /// 帧外或未填充时回退到实时值。
        /// </summary>
        TGrid FrameViewportTGrid => CurrentDrawingTargetContext is { ViewportTGrid: { } tGrid } ? tGrid : Editor.GetViewportTGrid();

        void RegisterSelectableObject(OngekiObjectBase obj, Vector2 centerPos, Vector2 size);

        bool CheckDrawingVisible(DrawingVisible visible);

        bool CheckVisible(TGrid tGrid);
        bool CheckRangeVisible(TGrid minTGrid, TGrid maxTGrid);

        double ConvertToY_DefaultSoflanGroup(TGrid tGrid) => ConvertToY(tGrid.TotalUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY_DefaultSoflanGroup(double tGridUnit) => ConvertToY(tGridUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToY(TGrid tGrid, SoflanList soflans) => ConvertToY(tGrid.TotalUnit, soflans);
        double ConvertToY(double tGridUnit, SoflanList soflans);

        double ConvertToViewRelativeY_DefaultSoflanGroup(TGrid tGrid) => ConvertToViewRelativeY(tGrid.TotalUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToViewRelativeY_DefaultSoflanGroup(double tGridUnit) => ConvertToViewRelativeY(tGridUnit, Editor.Fumen.SoflansMap.DefaultSoflanList);
        double ConvertToViewRelativeY(TGrid tGrid, SoflanList soflans) => ConvertToViewRelativeY(tGrid.TotalUnit, soflans);
        double ConvertToViewRelativeY(double tGridUnit, SoflanList soflans)
        {
            var worldY = ConvertToY(tGridUnit, soflans);
            return CurrentDrawingTargetContext is { } drawingTargetContext
                ? worldY - drawingTargetContext.ViewRelativeOriginY
                : worldY;
        }
    }
}
