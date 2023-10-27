using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
	public class DrawSelectableObjectTextureHelper
	{
		private IBatchTextureDrawing textureDrawing;
		private IHighlightBatchTextureDrawing highlightDrawing;

		private Dictionary<Texture, List<(Vector2 pos, Vector2 size, float)>> normalMap = new();
		private Dictionary<Texture, List<(Vector2 pos, Vector2 size, float)>> highlightMap = new();

		public DrawSelectableObjectTextureHelper()
		{
			textureDrawing = IoC.Get<IBatchTextureDrawing>();
			highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
		}

		List<(Vector2 pos, Vector2 size, float)> getList(Dictionary<Texture, List<(Vector2, Vector2, float)>> m, Texture t)
			=> m.TryGetValue(t, out var list) ? list : (m[t] = new());

		public void PostData(Vector2 pos, Vector2 size, Texture texture, Vector2? highlightPos = default, Vector2? highligtSize = default)
		{
			getList(normalMap, texture).Add((pos, size, 0));
			getList(highlightMap, texture).Add((highlightPos ?? pos, highligtSize ?? size, 0));
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
