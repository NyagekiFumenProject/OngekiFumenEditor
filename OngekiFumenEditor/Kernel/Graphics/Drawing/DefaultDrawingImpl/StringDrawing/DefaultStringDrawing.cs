using FontStashSharp;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String.Platform;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing
{
	[Export(typeof(IStringDrawing))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class DefaultStringDrawing : CommonDrawingBase, IStringDrawing, IDisposable
	{
		private Renderer renderer;

		private class FontHandle : IStringDrawing.FontHandle
		{
			public string Name { get; set; }
			public string FilePath { get; set; }
		}

		public static IEnumerable<IStringDrawing.FontHandle> DefaultSupportFonts { get; } = GetSupportFonts();
		public static IStringDrawing.FontHandle DefaultFont { get; } = GetSupportFonts().FirstOrDefault(x => x.Name.ToLower() == "consola");

		public IEnumerable<IStringDrawing.FontHandle> SupportFonts { get; } = DefaultSupportFonts;

		private static IEnumerable<IStringDrawing.FontHandle> GetSupportFonts()
		{
			return Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)).Select(x => new FontHandle
			{
				Name = Path.GetFileNameWithoutExtension(x),
				FilePath = x
			}).Where(x => Path.GetExtension(x.FilePath).ToLower() == ".ttf").ToArray();
		}

		public DefaultStringDrawing()
		{
			renderer = new Renderer();
		}

		Dictionary<IStringDrawing.FontHandle, FontSystem> cacheFonts = new Dictionary<IStringDrawing.FontHandle, FontSystem>();

		public FontSystem GetFontSystem(IStringDrawing.FontHandle fontHandle)
		{
			if (!cacheFonts.TryGetValue(fontHandle, out var fontSystem))
			{
				var handle = fontHandle as FontHandle;
				fontSystem = new FontSystem(new FontSystemSettings
				{
					FontResolutionFactor = 2,
					KernelWidth = 1,
					KernelHeight = 1,
					Effect = FontSystemEffect.Stroked
				});
				fontSystem.AddFont(File.ReadAllBytes(handle.FilePath));
				cacheFonts[fontHandle] = fontSystem;

				Log.LogDebug($"Created new FontSystem: {handle.Name}, FilePath: {handle.FilePath}");
			}

			return fontSystem;
		}

		public void Draw(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IDrawingContext target, IStringDrawing.FontHandle handle, out Vector2? measureTextSize)
		{
			target.PerfomenceMonitor.OnBeginDrawing(this);
			{
				handle = handle ?? DefaultFont;

				var fontStyle = TextStyle.None;

				IStringDrawing.FontHandle GetSubFont(IStringDrawing.FontHandle handle, string sub)
				{
					var boldFontName = handle.Name + "b";
					return SupportFonts.FirstOrDefault(x => x.Name == boldFontName);
				}

				if (style.HasFlag(IStringDrawing.StringStyle.Underline))
					fontStyle = TextStyle.Underline;
				if (style.HasFlag(IStringDrawing.StringStyle.Strike))
					fontStyle = TextStyle.Strikethrough;
				if (style.HasFlag(IStringDrawing.StringStyle.Bold))
				{
					if (GetSubFont(handle, "b") is IStringDrawing.FontHandle sb)
						handle = sb;
				}
				if (style.HasFlag(IStringDrawing.StringStyle.Italic))
				{
					if (GetSubFont(handle, "i") is IStringDrawing.FontHandle sb)
						handle = sb;
				}
				if (style.HasFlag(IStringDrawing.StringStyle.Italic) && style.HasFlag(IStringDrawing.StringStyle.Bold))
				{
					if (GetSubFont(handle, "z") is IStringDrawing.FontHandle sb)
						handle = sb;
				}

				renderer.Begin(GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target), target.PerfomenceMonitor, this);
				var font = GetFontSystem(handle).GetFont(fontSize);
				var size = font.MeasureString(text, scale);
				origin.X = origin.X * 2;
				origin = origin * size;
				scale.Y = -scale.Y;

				font.DrawText(renderer, text, pos, new FSColor(color.X, color.Y, color.Z, color.W), scale, rotate, origin, textStyle: fontStyle);
				measureTextSize = size;
				renderer.End();
			}
			target.PerfomenceMonitor.OnAfterDrawing(this);
		}

		public void Dispose()
		{
			foreach (var fs in cacheFonts)
				fs.Value?.Dispose();
			cacheFonts.Clear();

			renderer?.Dispose();
			renderer = null;
		}
	}
}
