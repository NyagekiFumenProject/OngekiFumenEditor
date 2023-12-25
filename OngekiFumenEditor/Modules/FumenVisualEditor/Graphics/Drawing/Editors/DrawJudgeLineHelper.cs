using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Graphics;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
	public class DrawJudgeLineHelper
	{
		private IStringDrawing stringDrawing;
		private ILineDrawing lineDrawing;
		private Vector4 color = new(1, 1, 0, 1);
		private Vector4 spdColor = new(FSColor.LightCyan.R / 255.0f, FSColor.LightCyan.G / 255.0f, FSColor.LightCyan.B / 255.0f, FSColor.LightCyan.A / 255.0f);

		LineVertex[] vertices = new LineVertex[2];

		public DrawJudgeLineHelper()
		{
			stringDrawing = IoC.Get<IStringDrawing>();
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
		}

		public void Draw(IFumenEditorDrawingContext target)
		{
			var y = (float)target.ConvertToY(target.Editor.GetCurrentTGrid().TotalUnit);

			vertices[0] = new(new(0, y), color, VertexDash.Solider);
			vertices[1] = new(new(target.ViewWidth, y), color, VertexDash.Solider);

			lineDrawing.Draw(target, vertices, 1);
			var t = target.Editor.GetCurrentTGrid();

			var bpmList = target.Editor.Fumen.BpmList;
			var soflanList = target.Editor.Fumen.Soflans;

			string str;
			if (target.Editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.AudioTime)
			{
				var audioTime = TGridCalculator.ConvertTGridToAudioTime(t, target.Editor);
				str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
			}
			else
				str = t.ToString();

			var speed = soflanList.CalculateSpeed(bpmList, t);

			stringDrawing.Draw(
					str,
					new(target.ViewWidth - 50,
					y + 10f),
					Vector2.One,
					12,
					0,
					color,
					new(1, 0.5f),
					IStringDrawing.StringStyle.Bold,
					target,
					default,
					out _
			);

			if (speed != 1)
			{
				var speedStr = $"{speed:F2}x";

				stringDrawing.Draw(
						speedStr,
						new(target.ViewWidth - 50,
						y - 10f),
						Vector2.One,
						12,
						0,
						spdColor,
						new(1, 1.5f),
						IStringDrawing.StringStyle.Bold,
						target,
						default,
						out _
				);
			}
		}
	}
}
