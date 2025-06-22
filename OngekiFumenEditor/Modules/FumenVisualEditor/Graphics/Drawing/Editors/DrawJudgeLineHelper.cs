using Caliburn.Micro;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects;
using System.Numerics;
using System.Windows.Media;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawJudgeLineHelper
    {
        private IStringDrawing stringDrawing;
        private ILineDrawing lineDrawing;
        private Vector4 color = new(1, 1, 0, 1);
        private Vector4 spdColor = new(Colors.LightCyan.R / 255.0f, Colors.LightCyan.G / 255.0f, Colors.LightCyan.B / 255.0f, Colors.LightCyan.A / 255.0f);

        LineVertex[] vertices = new LineVertex[2];

        public void Initalize(IRenderManagerImpl impl)
        {
            stringDrawing = impl.StringDrawing;
            lineDrawing = impl.SimpleLineDrawing;
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            var y = (float)target.ConvertToY_DefaultSoflanGroup(target.Editor.GetCurrentTGrid().TotalUnit);

            vertices[0] = new(new(0, y), color, VertexDash.Solider);
            vertices[1] = new(new(target.Editor.ViewWidth, y), color, VertexDash.Solider);

            lineDrawing.Draw(target, vertices, 1);
            var t = target.Editor.GetCurrentTGrid();

            var bpmList = target.Editor.Fumen.BpmList;

            string str;
            if (target.Editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.AudioTime)
            {
                var audioTime = TGridCalculator.ConvertTGridToAudioTime(t, target.Editor);
                str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
            }
            else
                str = t.ToString();

            stringDrawing.Draw(
                    str,
                    new(target.Editor.ViewWidth - 50,
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

            void PrintSpeed(int soflanGroup, SoflanList soflanList, Vector2 pos, Vector4 color)
            {
                var speed = soflanList.CalculateSpeed(bpmList, t);
                if (speed != 1)
                {
                    var speedStr = $"[{soflanGroup}]{speed:F2}x";

                    stringDrawing.Draw(
                            speedStr,
                            pos,
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

            //print default soflan group speed
            PrintSpeed(0, target.Editor.Fumen.SoflansMap.DefaultSoflanList, new(target.Editor.ViewWidth - 50, y - 10f), spdColor);

            //print specify soflan group speed
            if (IoC.Get<IFumenSoflanGroupListViewer>().CurrentSoflansDisplaySoflanGroupWrapItem is SoflanGroupWrapItem item)
            {
                var soflanGroup = item.SoflanGroupId;
                if (soflanGroup != 0)
                {
                    var soflanList = target.Editor.Fumen.SoflansMap[item.SoflanGroupId];
                    var color = IndividualSoflanAreaDrawingTarget.CalculateColorBySoflanGroup(item.SoflanGroupId);
                    PrintSpeed(item.SoflanGroupId, soflanList, new(target.Editor.ViewWidth - 100, y - 10f), color);
                }
            }
        }
    }
}
