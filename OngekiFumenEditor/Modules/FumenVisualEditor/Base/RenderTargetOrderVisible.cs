using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public class RenderTargetOrderVisibleConfig
    {
        public Dictionary<string, RenderTargetOrderVisible> map = new();
    }

    public class RenderTargetOrderVisible
    {
        public int Order { get; set; }
        public DrawingVisible Visible { get; set; }
    }
}
