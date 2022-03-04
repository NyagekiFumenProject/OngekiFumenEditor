using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(DummyLinesDrawTargetBase))]
    public class DummyLinesDrawTargetBase : CommonLinesDrawTargetBase<ConnectableObjectBase>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "SB" };

        public override void FillLine(List<LinePoint> lines, ConnectableObjectBase obj, OngekiFumen fumen)
        {
            lines.Add(new(new(0, 0), new(1, 0, 0, 1)));
            lines.Add(new(new(0, 200), new(0, 1, 0, 1)));
            lines.Add(new(new(200, 0), new(0, 0, 1, 1)));
            lines.Add(new(new(2200, 0), new(0, 0, 1, 1)));
            lines.Add(new(new(20, 50), new(0, 0, 1, 1)));
            lines.Add(new(new(280, 400), new(0, 0, 1, 1)));
            lines.Add(new(new(0, 0), new(1, 0, 0, 1)));
        }
    }
}
