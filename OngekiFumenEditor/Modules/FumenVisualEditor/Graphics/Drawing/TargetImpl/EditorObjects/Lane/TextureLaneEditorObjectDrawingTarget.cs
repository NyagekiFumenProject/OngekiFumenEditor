using OngekiFumenEditor.Kernel.Graphics.Base;
using System;
using System.Drawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
	public abstract class TextureLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
	{
		public static Texture LoadTextrueFromDefaultResource(string rPath)
		{
			var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
			using var bitmap = Image.FromStream(info.Stream) as Bitmap;
			return new Texture(bitmap);
		}

		public override Texture StartEditorTexture { get; }
		public override Texture NextEditorTexture { get; }
		public override Texture EndEditorTexture { get; }

		public TextureLaneEditorObjectDrawingTarget(Texture startEditorTexture, Texture nextEditorTexture, Texture endEditorTexture)
		{
			StartEditorTexture = startEditorTexture;
			NextEditorTexture = nextEditorTexture;
			EndEditorTexture = endEditorTexture;
		}
	}
}
