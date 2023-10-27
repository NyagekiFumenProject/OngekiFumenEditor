using OpenTK.Mathematics;
using OpenTK.Wpf;
using System;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawingContext
	{
		/// <summary>
		/// 左手坐标系为世界坐标系的可视区域
		/// </summary>
		public struct VisibleRect
		{
			public VisibleRect(Vector2 buttomRight, Vector2 topLeft)
			{
				TopLeft = topLeft;
				ButtomRight = buttomRight;
			}

			public Vector2 TopLeft { get; init; }
			public Vector2 ButtomRight { get; init; }

			public float Width => ButtomRight.X - TopLeft.X;
			public float Height => TopLeft.Y - ButtomRight.Y;

			public float MinY => ButtomRight.Y;
			public float MaxY => TopLeft.Y;

			public float MinX => TopLeft.X;
			public float MaxX => ButtomRight.X;
		}

		//values are updating by frame
		public VisibleRect Rect { get; }

		public float ViewWidth => Rect.Width;
		public float ViewHeight => Rect.Height;

		Matrix4 ProjectionMatrix { get; }
		Matrix4 ViewMatrix { get; }
		Matrix4 ViewProjectionMatrix { get; }

		IPerfomenceMonitor PerfomenceMonitor { get; }

		void PrepareRender(GLWpfControl glView);
		void OnRenderSizeChanged(GLWpfControl glView, SizeChangedEventArgs e);

		void Render(TimeSpan ts);
	}
}
