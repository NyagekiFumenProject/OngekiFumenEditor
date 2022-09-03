using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane
{
    [Export(typeof(IDrawingTarget))]
    internal class WallLaneDrawTarget : LaneDrawingTargetBase
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WLS", "WRS" };

        private Vector4 LeftWallColor = new Vector4(35 / 255.0f, 4 / 255.0f, 117 / 255.0f, 255 / 255.0f);
        private Vector4 RightWallColor = new Vector4(136 / 255.0f, 3 / 255.0f, 152 / 255.0f, 255 / 255.0f);

        public WallLaneDrawTarget()
        {
            LineWidth = 6;
        }

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj, OngekiFumen fumen)
        {
            if (obj.IDShortName[1] == 'L')
                return LeftWallColor;
            return RightWallColor;
        }
    }
}
