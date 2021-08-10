using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ToolboxItems
{
    public class BellToolboxItem : ElementViewModel
    {
        public Bell Bell { get; set; }

        public BellToolboxItem(Bell bell)
        {
            this.Bell = bell;
            Name = bell.Name;
        }
    }
}
