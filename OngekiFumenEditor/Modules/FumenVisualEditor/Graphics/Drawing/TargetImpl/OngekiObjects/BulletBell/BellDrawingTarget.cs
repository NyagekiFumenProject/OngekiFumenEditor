using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class BellDrawingTarget : BulletPalleteReferencableBatchDrawTargetBase<Bell>
	{
		private readonly Texture texture;
		private readonly Vector2 size;

		public BellDrawingTarget()
		{
			Texture LoadTex(string rPath)
			{
				var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
				using var bitmap = Image.FromStream(info.Stream) as Bitmap;
				return new Texture(bitmap);
			}

			texture = LoadTex("bell.png");
			size = new Vector2(40, 40);
			normalDrawList[texture] = new();
			selectedDrawList[texture] = new();
		}

		public override IEnumerable<string> DrawTargetID { get; } = new[] { "BEL" };
		public override int DefaultRenderOrder => 1000;

		public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
		{
			var offsetPos = pos;
			normalDrawList[texture].Add((size, offsetPos, 0));
			if (obj.IsSelected)
				selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
			drawStrList.Add((offsetPos, obj));
			target.RegisterSelectableObject(obj, offsetPos, size);
		}

		public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
		{
			var offsetPos = pos;
			normalDrawList[texture].Add((size, offsetPos, 0));
			if (obj.IsSelected)
				selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
		}
	}
}
