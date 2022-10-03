using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawJudgeLineHelper
    {
        private IStringDrawing stringDrawing;
        private ILineDrawing lineDrawing;
        private Vector4 color = new(0, 1, 1, 1);

        LineVertex[] vertices = new LineVertex[2];

        public DrawJudgeLineHelper()
        {
            stringDrawing = IoC.Get<IStringDrawing>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            var y = target.CurrentPlayTime;

            vertices[0] = new(new(0, y), color);
            vertices[1] = new(new(target.ViewWidth, y), color);

            lineDrawing.Draw(target, vertices, 1);

            var str = string.Empty;
            var t = target.Editor.GetCurrentJudgeLineTGrid();
            if (target.Editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.AudioTime)
            {
                var audioTime = TGridCalculator.ConvertTGridToAudioTime(t, target.Editor);
                str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
            }
            else
                str = t.ToString();

            stringDrawing.Draw(
                    str,
                    new(target.ViewWidth - 50,
                    y + 10f),
                    Vector2.One,
                    12,
                    0,
                    color,
                    new(1, 0.5f),
                    IStringDrawing.StringStyle.Normal,
                    target,
                    default,
                    out _
            );
        }
    }
}
