using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawSelectableObjectTextureHelper
	{
		private IBatchTextureDrawing textureDrawing;
		private IHighlightBatchTextureDrawing highlightDrawing;

		private Dictionary<IImage, List<(Vector2 pos, Vector2 size, float, Vector4)>> normalMap = new();
		private Dictionary<IImage, List<(Vector2 pos, Vector2 size, float, Vector4)>> highlightMap = new();

		public DrawSelectableObjectTextureHelper()
		{
			textureDrawing = IoC.Get<IRenderManager>().BatchTextureDrawing;
			highlightDrawing = IoC.Get<IRenderManager>().HighlightBatchTextureDrawing;
		}

		List<(Vector2 pos, Vector2 size, float, Vector4)> getList(Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>> m, IImage t)
			=> m.TryGetValue(t, out var list) ? list : (m[t] = new());

		public void PostData(Vector2 pos, Vector2 size, IImage texture, Vector2? highlightPos = default, Vector2? highligtSize = default)
		{
			getList(normalMap, texture).Add((pos, size, 0, Vector4.One));
			getList(highlightMap, texture).Add((highlightPos ?? pos, highligtSize ?? size, 0, Vector4.One));
		}

		public void Draw(IFumenEditorDrawingContext target)
		{
			foreach (var t in highlightMap.Keys)
			{
				var list = getList(highlightMap, t);
				highlightDrawing.Draw(target, t, list);
				list.Clear();
			}

			foreach (var t in normalMap.Keys)
			{
				var list = getList(normalMap, t);
				textureDrawing.Draw(target, t, list);
				list.Clear();
			}
		}
	}
}
