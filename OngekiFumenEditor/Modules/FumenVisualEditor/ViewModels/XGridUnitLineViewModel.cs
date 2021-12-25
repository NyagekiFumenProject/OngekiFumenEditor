using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public class XGridUnitLineViewModel
    {
        public double X { get; set; }
        public double Unit { get; set; }
        public bool IsCenterLine { get; set; }
        public override string ToString() => $"{X:F4} {Unit} {(IsCenterLine ? "Center" : string.Empty)}";
    }
}
