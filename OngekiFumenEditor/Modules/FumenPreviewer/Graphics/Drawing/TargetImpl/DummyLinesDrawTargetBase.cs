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
        public override string DrawTargetID => "SB";

        public override void FillLine(Action<Vector2, Vector4> appendFunc)
        {
            appendFunc(new(0, 0), new(1, 0, 0, 1));
            appendFunc(new(0, 200), new(0, 1, 0, 1));
            appendFunc(new(200, 0), new(0, 0, 1, 1));
            appendFunc(new(0, 0), new(1, 0, 0, 1));
        }
    }
}
