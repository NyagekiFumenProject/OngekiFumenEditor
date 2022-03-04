using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(DummyDrawTarget))]
    public class DummyDrawTarget : CommonSpriteDrawTargetBase<Bell>
    {
        public override string DrawTargetID => Bell.CommandName;

        public DummyDrawTarget() : base(new Texture(Properties.Resources.bell))
        {

        }

        protected override Vector GetObjectPosition(Bell obj, OngekiFumen fumen)
        {
            return new(100, 100);
        }
    }
}
