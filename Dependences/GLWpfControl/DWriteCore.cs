using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Vortice.DirectWrite;

namespace OpenTK.Wpf
{
	public static class DWriteCore
	{
		private static Vortice.DirectWrite.IDWriteFactory _DWriteFactory = Vortice.DirectWrite.DWrite.DWriteCreateFactory<Vortice.DirectWrite.IDWriteFactory>();

		private static DComp CurrentDComp;

		private static Dictionary<DComp, Queue<Action<Vortice.Direct2D1.ID2D1DeviceContext, float>>> DcompDWriteCommandQueue = new();

		//private static Dictionary<Vortice.DirectWrite.IDWriteTextFormat> formats;

		public static void SetCurrent(DComp DComp)
		{
			CurrentDComp = DComp;
			if (!DcompDWriteCommandQueue.ContainsKey(DComp))
			{
				DcompDWriteCommandQueue.Add(DComp, new());
			}
		}

		public static Queue<Action<Vortice.Direct2D1.ID2D1DeviceContext, float>> GetCommands(DComp DComp)
		{
			return DcompDWriteCommandQueue[DComp];
		}

		public enum StringStyle
		{
			Normal = 0,
			Bold = 1,
			Italic = 2,
			Overline = 4,
			Strike = 8,
			Underline = 16,
		}

		public static void DrawRaw(string text, Vector2 pos, int fontSize, Vector4 color, Vortice.DirectWrite.FontStyle fontStyle, Vortice.DirectWrite.FontWeight fontWeight)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			DcompDWriteCommandQueue[CurrentDComp].Enqueue((rt, height) =>
			{
				var format = _DWriteFactory.CreateTextFormat("Cascadia Mono", fontWeight, fontStyle, fontSize);
				var layout = _DWriteFactory.CreateTextLayout(text, format, float.PositiveInfinity, float.PositiveInfinity);
				var brush = rt.CreateSolidColorBrush(new Vortice.Mathematics.Color4(color));
				rt.DrawTextLayout(pos, layout, brush);
				brush.Dispose();
				layout.Dispose();
				format.Dispose();
			});
		}

		public static void Draw(string text, Vector2 pos, int fontSize, Vector4 color, Vector2 origin, dynamic target, int StringStyle)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			var style = (StringStyle)StringStyle;
			Vortice.DirectWrite.FontStyle fontStyle = style.HasFlag(DWriteCore.StringStyle.Italic) ? Vortice.DirectWrite.FontStyle.Italic : Vortice.DirectWrite.FontStyle.Normal;
			Vortice.DirectWrite.FontWeight fontWeight = style.HasFlag(DWriteCore.StringStyle.Bold) ? Vortice.DirectWrite.FontWeight.Bold : Vortice.DirectWrite.FontWeight.Normal;
			DcompDWriteCommandQueue[CurrentDComp].Enqueue((rt, height) =>
			{
				var format = _DWriteFactory.CreateTextFormat("Cascadia Mono", fontWeight, fontStyle, fontSize);
				var layout = _DWriteFactory.CreateTextLayout(text, format, float.PositiveInfinity, float.PositiveInfinity);
				var brush = rt.CreateSolidColorBrush(new Vortice.Mathematics.Color4(color));
				rt.DrawTextLayout(new(pos.X - origin.X * layout.Metrics.WidthIncludingTrailingWhitespace + +target.MinX, height - (pos.Y + origin.Y * layout.Metrics.Height) + target.MinY), layout, brush);
				brush.Dispose();
				layout.Dispose();
				format.Dispose();
			});
		}

		public static Vector2 Measure(string text, int fontSize, int StringStyle)
		{
			if (string.IsNullOrEmpty(text))
			{
				return Vector2.Zero;
			}
			var style = (StringStyle)StringStyle;
			Vortice.DirectWrite.FontStyle fontStyle = style.HasFlag(DWriteCore.StringStyle.Italic) ? Vortice.DirectWrite.FontStyle.Italic : Vortice.DirectWrite.FontStyle.Normal;
			Vortice.DirectWrite.FontWeight fontWeight = style.HasFlag(DWriteCore.StringStyle.Bold) ? Vortice.DirectWrite.FontWeight.Bold : Vortice.DirectWrite.FontWeight.Normal;
			var format = _DWriteFactory.CreateTextFormat("Cascadia Mono", fontWeight, fontStyle, fontSize);
			var layout = _DWriteFactory.CreateTextLayout(text, format, float.PositiveInfinity, float.PositiveInfinity);
			var result = new Vector2(layout.Metrics.Width, layout.Metrics.Height);
			layout.Dispose();
			format.Dispose();
			return result;
		}
	}
}
