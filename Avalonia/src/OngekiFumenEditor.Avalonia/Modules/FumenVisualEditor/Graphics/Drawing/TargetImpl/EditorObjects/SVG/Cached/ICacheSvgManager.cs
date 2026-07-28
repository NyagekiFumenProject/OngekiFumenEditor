using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using System.Collections.Generic;
using Avalonia;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached
{
	public interface ICachedSvgRenderDataManager
	{
		public List<LineVertex> GetRenderData(IDrawingContext target, SvgPrefabBase svgPrefab, out bool isCached, out Rect bound);
	}
}

