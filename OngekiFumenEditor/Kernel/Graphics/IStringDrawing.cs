﻿using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IStringDrawing : IDrawing
	{
		public enum StringStyle
		{
			Normal = 0,
			Bold = 1,
			Italic = 2,
			Overline = 4,
			Strike = 8,
			Underline = 16,
		}

		public interface IFontHandle
		{
			string FamilyName { get; }
		}

		IEnumerable<IFontHandle> SupportFonts { get; }

		void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, StringStyle style, IDrawingContext target, IFontHandle handle, out Vector2? measureTextSize);
	}
}
